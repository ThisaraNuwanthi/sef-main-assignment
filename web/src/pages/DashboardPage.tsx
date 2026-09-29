import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router'
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { api } from '../api/client'
import type { EnrolmentSummary } from '../api/types'
import { Empty, ErrorAlert, Loading } from '../components/States'
import { humanize, lkr, ms } from '../utils/format'

export function DashboardPage() {
  const { data, isPending, error } = useQuery({
    queryKey: ['reports', 'enrolment-summary'],
    queryFn: () => api<EnrolmentSummary>('/api/reports/enrolment-summary'),
  })

  if (isPending) return <Loading label="Loading dashboard…" />
  if (error) return <ErrorAlert error={error} title="Could not load the dashboard" />

  const count = (status: string) => data.byStatus.find((s) => s.status === status)?.count ?? 0
  const thisMonth = data.monthlyFees[data.monthlyFees.length - 1]

  return (
    <section aria-labelledby="dash-title">
      <h1 id="dash-title">Dashboard</h1>

      <div className="cards">
        <SummaryCard label="Total enrolments" value={data.totalEnrolments} />
        <SummaryCard
          label="Waiting for approval"
          value={count('PendingAdminApproval')}
          link={count('PendingAdminApproval') > 0 ? '/admin/enrolments?status=PendingAdminApproval' : undefined}
          highlight={count('PendingAdminApproval') > 0}
        />
        <SummaryCard label="Approved" value={count('Approved')} />
        <SummaryCard
          label="Failed workflows"
          value={data.agents.failed}
          link={data.agents.failed > 0 ? '/admin/enrolments?status=Failed' : undefined}
        />
        <SummaryCard label="Fees this month" value={lkr(thisMonth?.total ?? 0)} />
        <SummaryCard label="Avg. agent time" value={ms(data.agents.averageDurationMs)} />
      </div>

      <div className="grid-2">
        <div className="card">
          <h2>Class fill rate (%)</h2>
          {data.classFill.length === 0 ? (
            <Empty message="No active classes yet." />
          ) : (
            // role="img" + aria-label gives screen readers a summary of the chart.
            <div className="chart" role="img" aria-label={data.classFill.map((c) => `${c.className} ${c.fillRatePercent}%`).join(', ')}>
              <ResponsiveContainer width="100%" height={260}>
                <BarChart data={data.classFill} margin={{ top: 8, right: 8, left: -16, bottom: 40 }}>
                  <CartesianGrid strokeDasharray="3 3" vertical={false} />
                  <XAxis dataKey="className" angle={-30} textAnchor="end" interval={0} fontSize={12} />
                  <YAxis domain={[0, 100]} fontSize={12} />
                  <Tooltip formatter={(v: number) => `${v}%`} />
                  <Bar dataKey="fillRatePercent" name="Fill rate" fill="#2f6f4f" radius={[4, 4, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </div>
          )}
        </div>

        <div className="card">
          <h2>Enrolments by status</h2>
          <table className="table compact">
            <caption className="sr-only">Number of enrolments in each status</caption>
            <tbody>
              {data.byStatus.map((s) => (
                <tr key={s.status}>
                  <th scope="row">{humanize(s.status)}</th>
                  <td className="num">{s.count}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      <div className="card">
        <h2>Monthly fee totals</h2>
        <table className="table compact">
          <thead>
            <tr>
              <th scope="col">Month</th>
              <th scope="col" className="num">Fee records</th>
              <th scope="col" className="num">Total</th>
            </tr>
          </thead>
          <tbody>
            {data.monthlyFees.map((m) => (
              <tr key={m.month}>
                <td>{m.month}</td>
                <td className="num">{m.records}</td>
                <td className="num">{lkr(m.total)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}

function SummaryCard({ label, value, link, highlight }: { label: string; value: string | number; link?: string; highlight?: boolean }) {
  const body = (
    <>
      <span className="card-label">{label}</span>
      <span className="card-value">{value}</span>
    </>
  )
  return link ? (
    <Link to={link} className={`card stat${highlight ? ' stat-highlight' : ''}`}>{body}</Link>
  ) : (
    <div className="card stat">{body}</div>
  )
}
