// k6 performance test for the SKCA Enrol API.
//
//   k6 run perf/load-test.js                                   (API on http://localhost:5056)
//   k6 run -e API_BASE_URL=http://localhost:5057 perf/load-test.js
//
// Two things happen at the same time:
//   1. browse_classes:     20 virtual users keep listing classes for 30 s (read load).
//   2. submit_enrolments:  10 parents each submit one enrolment and wait until the agent
//                          workflow finishes, measuring end-to-end agent latency.
//
// Run it against a LOCAL API with Llm:Provider=Fake. Against production it would create
// test accounts in the real database and spend the Gemini free quota.

import http from 'k6/http'
import exec from 'k6/execution'
import { check, sleep } from 'k6'
import { Rate, Trend } from 'k6/metrics'

const BASE = __ENV.API_BASE_URL || 'http://localhost:5056'
const PARENTS = 10

// Custom metrics shown in the summary next to k6's built-in http_req_duration.
const workflowLatency = new Trend('agent_workflow_latency_ms', true)
const enrolmentSuccess = new Rate('enrolment_success')

export const options = {
  scenarios: {
    browse_classes: { executor: 'constant-vus', vus: 20, duration: '30s', exec: 'browseClasses' },
    submit_enrolments: { executor: 'per-vu-iterations', vus: PARENTS, iterations: 1, maxDuration: '2m', exec: 'submitEnrolment' },
  },
  thresholds: {
    'http_req_duration{scenario:browse_classes}': ['p(95)<500'], // reads stay fast under load
    'http_req_failed{scenario:browse_classes}': ['rate<0.01'],
    enrolment_success: ['rate>0.95'], // almost every request reaches admin review
    agent_workflow_latency_ms: ['p(95)<30000'],
  },
}

const json = { headers: { 'Content-Type': 'application/json' } }
const auth = (token) => ({ headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` } })

// Runs once before the test: create fresh parents, each with one child.
export function setup() {
  const run = Date.now()
  const parents = []
  for (let i = 0; i < PARENTS; i++) {
    const res = http.post(`${BASE}/api/auth/register`,
      JSON.stringify({ fullName: `Load Parent ${i}`, email: `load${run}-${i}@perf.test`, password: 'LoadTest123' }), json)
    const token = res.json('token')
    const child = http.post(`${BASE}/api/children`,
      JSON.stringify({ fullName: `Load Child ${i}`, dateOfBirth: '2016-06-01' }), auth(token))
    parents.push({ token, childId: child.json('id') })
  }
  return { parents }
}

export function browseClasses(data) {
  const { token } = data.parents[exec.vu.idInTest % PARENTS]
  const res = http.get(`${BASE}/api/classes?level=Beginner&sortBy=seatsLeft&page=1&pageSize=10`, auth(token))
  check(res, { 'classes 200': (r) => r.status === 200 })
  sleep(0.2)
}

export function submitEnrolment(data) {
  const parent = data.parents[exec.scenario.iterationInTest]
  const started = Date.now()
  const created = http.post(`${BASE}/api/enrolments`, JSON.stringify({
    childId: parent.childId,
    preferredDays: ['Saturday', 'Sunday', 'Wednesday'],
    parentNotes: 'Load test request',
  }), auth(parent.token))
  if (!check(created, { 'enrolment 201': (r) => r.status === 201 })) {
    enrolmentSuccess.add(false)
    return
  }

  // Poll until the agents finish (the workflow runs in the background worker).
  const id = created.json('enrolmentId')
  let status = 'Submitted'
  while (['Submitted', 'AgentProcessing'].includes(status) && Date.now() - started < 90000) {
    sleep(0.5)
    status = http.get(`${BASE}/api/enrolments/${id}`, auth(parent.token)).json('status')
  }
  workflowLatency.add(Date.now() - started)
  enrolmentSuccess.add(status === 'PendingAdminApproval')
}
