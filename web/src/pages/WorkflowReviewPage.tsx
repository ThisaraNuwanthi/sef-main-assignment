import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate, useParams } from 'react-router'
import { api } from '../api/client'
import type { AgentStep, Workflow } from '../api/types'
import { ErrorAlert, Loading, SuccessAlert } from '../components/States'
import { StatusBadge } from '../components/StatusBadge'
import { dateTime, humanize, lkr, ms } from '../utils/format'

type Decision = 'approve' | 'reject' | 'revise'

const decisionLabel: Record<Decision, string> = {
  approve: 'approved',
  reject: 'rejected',
  revise: 'sent back to the parent for revision',
}

/**
 * The Admin's review screen for one agent workflow: what the agents did, why,
 * and the human decision (approve / reject / request revision).
 */
export function WorkflowReviewPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [note, setNote] = useState('')
  const [noteError, setNoteError] = useState<string | null>(null)
  const [done, setDone] = useState<string | null>(null)

  const { data: wf, isPending, error } = useQuery({
    queryKey: ['workflow', id],
    queryFn: () => api<Workflow>(`/api/workflows/${id}`),
    // Poll every 2 s while the agents are still working, then stop.
    refetchInterval: (query) => (['Queued', 'Running'].includes(query.state.data?.status ?? '') ? 2000 : false),
  })

  const decide = useMutation({
    mutationFn: (decision: Decision) =>
      api<Workflow>(`/api/workflows/${id}/${decision}`, { method: 'POST', body: { note: note.trim() || null } }),
    onSuccess: (updated, decision) => {
      queryClient.setQueryData(['workflow', id], updated) // show the new state without refetching
      queryClient.invalidateQueries({ queryKey: ['enrolments'] })
      queryClient.invalidateQueries({ queryKey: ['reports'] })
      setDone(`The request was ${decisionLabel[decision]}.`)
      setNote('')
    },
  })

  const retry = useMutation({
    mutationFn: () => api<{ newWorkflowId: number }>(`/api/workflows/${id}/retry`, { method: 'POST' }),
    onSuccess: (result) => navigate(`/admin/workflows/${result.newWorkflowId}`),
  })

  const submit = (decision: Decision) => {
    setDone(null)
    // Reject and revise must explain why; the parent sees this note in the app.
    if (decision !== 'approve' && !note.trim()) {
      setNoteError('Please write a note so the parent knows why.')
      return
    }
    setNoteError(null)
    decide.mutate(decision)
  }

  if (isPending) return <Loading label="Loading workflow…" />
  if (error) return <ErrorAlert error={error} title="Could not load the workflow" />

  const canDecide = wf.status === 'AwaitingApproval' && wf.enrolmentStatus === 'PendingAdminApproval'
  const warnings = wf.validationResults.filter((v) => !v.passed && v.severity === 'Warning')

  return (
    <section aria-labelledby="wf-title">
      <p><Link to="/admin/enrolments">← Back to enrolments</Link></p>
      <div className="page-head">
        <h1 id="wf-title">Review: {wf.childName}</h1>
        <div className="badges">
          <span>Workflow #{wf.id} <StatusBadge status={wf.status} /></span>
          <span>Enrolment <StatusBadge status={wf.enrolmentStatus} /></span>
        </div>
      </div>

      {['Queued', 'Running'].includes(wf.status) && <Loading label="The agents are working on this request… (updates automatically)" />}
      {wf.failureReason && (
        <div className="alert alert-error" role="alert">
          <strong>Workflow failed safely.</strong> {wf.failureReason}
        </div>
      )}
      {warnings.map((w) => (
        <div key={w.ruleName} className="alert alert-warn" role="alert">
          <strong>⚠ {humanize(w.ruleName)}:</strong> {w.message}
        </div>
      ))}

      <div className="grid-2">
        <div className="card">
          <h2>Request</h2>
          <dl className="facts">
            <dt>Parent</dt><dd>{wf.parentName}</dd>
            <dt>Objective</dt><dd>{wf.objective}</dd>
            <dt>Parent notes</dt>
            {/* Rendered as plain text (React escapes it): notes are untrusted input. */}
            <dd>{wf.parentNotes ? <q className="notes">{wf.parentNotes}</q> : <span className="muted">None</span>}</dd>
            <dt>Timing</dt>
            <dd>Started {dateTime(wf.startedAt)} · total {ms(wf.totalDurationMs)}</dd>
          </dl>
        </div>

        <div className="card">
          <h2>Proposal</h2>
          {wf.proposal ? (
            <dl className="facts">
              <dt>Class</dt><dd><strong>{wf.proposal.className}</strong></dd>
              <dt>Assessed level</dt>
              <dd>{wf.proposal.assessedLevel} ({wf.proposal.skillConfidence} confidence) — {wf.proposal.skillRationale}</dd>
              <dt>Why this class</dt><dd>{wf.proposal.placementReason}</dd>
              {wf.proposal.levelToleranceReason && (<><dt>Level tolerance</dt><dd>{wf.proposal.levelToleranceReason}</dd></>)}
              <dt>Monthly fee</dt>
              <dd>{lkr(wf.proposal.feeAmount)}{wf.proposal.siblingDiscountApplied && ' (10% sibling discount applied)'}</dd>
            </dl>
          ) : (
            <p className="muted">No proposal{wf.status === 'Failed' ? ' — the workflow stopped before one was made.' : ' yet.'}</p>
          )}
        </div>
      </div>

      {/* ---------- Human decision ---------- */}
      <div className="card decision" aria-labelledby="decision-title">
        <h2 id="decision-title">Decision</h2>
        {done && <SuccessAlert message={done} />}
        {decide.error && <ErrorAlert error={decide.error} title="The decision was not saved" />}
        {retry.error && <ErrorAlert error={retry.error} title="Could not retry" />}

        {canDecide ? (
          <>
            <label htmlFor="note">Note (required to reject or request a revision; the parent will see it)</label>
            <textarea
              id="note"
              rows={3}
              maxLength={500}
              value={note}
              onChange={(e) => setNote(e.target.value)}
              aria-invalid={!!noteError}
              aria-describedby={noteError ? 'note-error' : undefined}
            />
            {noteError && <p id="note-error" className="field-error">{noteError}</p>}
            <div className="form-actions">
              <button type="button" className="btn-primary" disabled={decide.isPending} onClick={() => submit('approve')}>Approve</button>
              <button type="button" className="btn-secondary" disabled={decide.isPending} onClick={() => submit('revise')}>Request revision</button>
              <button type="button" className="btn-danger" disabled={decide.isPending} onClick={() => submit('reject')}>Reject</button>
            </div>
          </>
        ) : wf.status === 'Failed' && wf.enrolmentStatus === 'Failed' ? (
          <button type="button" className="btn-primary" disabled={retry.isPending} onClick={() => retry.mutate()}>
            {retry.isPending ? 'Retrying…' : 'Retry workflow'}
          </button>
        ) : (
          <p className="muted">No decision needed in this state.</p>
        )}

        {wf.decisions.length > 0 && (
          <ul className="decisions">
            {wf.decisions.map((d) => (
              <li key={d.decidedAt}>
                <StatusBadge status={d.decision} /> by {d.adminName} on {dateTime(d.decidedAt)}{d.note && <> — “{d.note}”</>}
              </li>
            ))}
          </ul>
        )}
      </div>

      {/* ---------- What the agents did ---------- */}
      <div className="grid-2">
        <div className="card">
          <h2>Plan</h2>
          {wf.plan ? (
            <>
              <ol>{wf.plan.steps.map((s) => <li key={s}>{humanize(s)}</li>)}</ol>
              {wf.plan.summary && <p className="muted">{wf.plan.summary}</p>}
            </>
          ) : <p className="muted">No plan yet.</p>}
        </div>

        <div className="card">
          <h2>Validation results</h2>
          {wf.validationResults.length === 0 ? <p className="muted">Not validated yet.</p> : (
            <table className="table compact">
              <caption className="sr-only">Validation rules checked by the ValidationSafetyAgent</caption>
              <thead><tr><th scope="col">Rule</th><th scope="col">Result</th><th scope="col">Details</th></tr></thead>
              <tbody>
                {wf.validationResults.map((v) => (
                  <tr key={v.ruleName}>
                    <td>{humanize(v.ruleName)}</td>
                    <td className={v.passed ? 'text-good' : v.severity === 'Warning' ? 'text-warn' : 'text-bad'}>
                      {v.passed ? '✓ Pass' : v.severity === 'Warning' ? '⚠ Warning' : '✗ Fail'}
                    </td>
                    <td>{v.message}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>

      <div className="card">
        <h2>Agent steps</h2>
        <ol className="steps">
          {wf.steps.map((step) => <StepItem key={step.id} step={step} />)}
        </ol>
      </div>
    </section>
  )
}

function StepItem({ step }: { step: AgentStep }) {
  return (
    <li className="step">
      <div className="step-head">
        <strong>{step.stepNo}. {humanize(step.stepName)}</strong>
        <span className="muted">{step.agentName}</span>
        <StatusBadge status={step.status} />
        <span className="muted">{ms(step.durationMs)}{step.retryCount > 0 && ` · ${step.retryCount} retr${step.retryCount === 1 ? 'y' : 'ies'}`}</span>
      </div>
      {step.error && <p className="text-bad">Error: {step.error}</p>}

      {step.toolCalls.length > 0 && (
        <table className="table compact">
          <caption>Tool calls</caption>
          <thead><tr><th scope="col">Tool</th><th scope="col">Result</th><th scope="col">Time</th><th scope="col">Error</th></tr></thead>
          <tbody>
            {step.toolCalls.map((t) => (
              <tr key={t.id}>
                <td>{t.toolName}</td>
                <td className={t.success ? 'text-good' : 'text-bad'}>{t.success ? 'OK' : 'Failed'}</td>
                <td>{ms(t.durationMs)}</td>
                <td>{t.error ?? '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <details>
        <summary>Input and output</summary>
        <div className="grid-2">
          <div><h3>Input</h3><pre>{JSON.stringify(step.input, null, 2)}</pre></div>
          <div><h3>Output</h3><pre>{step.output ? JSON.stringify(step.output, null, 2) : '—'}</pre></div>
        </div>
      </details>
    </li>
  )
}
