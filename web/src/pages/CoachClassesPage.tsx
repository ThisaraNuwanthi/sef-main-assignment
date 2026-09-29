import { useQuery } from '@tanstack/react-query'
import { api } from '../api/client'
import type { CoachClass } from '../api/types'
import { Empty, ErrorAlert, Loading } from '../components/States'
import { hhmm } from '../utils/format'

/** Read-only: the coach's own classes and who is in them. */
export function CoachClassesPage() {
  const { data, isPending, error } = useQuery({
    queryKey: ['my-classes'],
    queryFn: () => api<CoachClass[]>('/api/classes/mine'),
  })

  if (isPending) return <Loading label="Loading your classes…" />
  if (error) return <ErrorAlert error={error} title="Could not load your classes" />

  return (
    <section aria-labelledby="coach-title">
      <h1 id="coach-title">My classes</h1>
      {data.length === 0 ? (
        <Empty message="You have no active classes." />
      ) : (
        data.map(({ class: c, students }) => (
          <article key={c.id} className="card" aria-labelledby={`class-${c.id}`}>
            <div className="page-head">
              <h2 id={`class-${c.id}`}>{c.name}</h2>
              <span className="muted">
                {c.level} · {c.dayOfWeek} {hhmm(c.startTime)}–{hhmm(c.endTime)} · {c.seatsTaken}/{c.capacity} seats
              </span>
            </div>
            {students.length === 0 ? (
              <Empty message="No students placed yet." />
            ) : (
              <div className="table-wrap">
                <table className="table compact">
                  <caption className="sr-only">Students in {c.name}</caption>
                  <thead>
                    <tr>
                      <th scope="col">Student</th>
                      <th scope="col" className="num">Age</th>
                      <th scope="col">Lichess</th>
                      <th scope="col">Parent</th>
                    </tr>
                  </thead>
                  <tbody>
                    {students.map((s) => (
                      <tr key={s.childId}>
                        <td>{s.childName}</td>
                        <td className="num">{s.age}</td>
                        <td>
                          {s.lichessUsername ? (
                            <a href={`https://lichess.org/@/${encodeURIComponent(s.lichessUsername)}`} target="_blank" rel="noreferrer">
                              {s.lichessUsername}
                            </a>
                          ) : '—'}
                        </td>
                        <td>{s.parentName}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </article>
        ))
      )}
    </section>
  )
}
