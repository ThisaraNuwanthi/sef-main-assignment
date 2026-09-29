import { useState } from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useLocation } from 'react-router'
import { api, toQuery } from '../api/client'
import { DAYS, LEVELS, type ChessClass, type PagedResult } from '../api/types'
import { Pagination } from '../components/Pagination'
import { Empty, ErrorAlert, Loading, SuccessAlert } from '../components/States'
import { hhmm, lkr } from '../utils/format'

interface Filters {
  search: string
  level: string
  day: string
  sortBy: string
  desc: boolean
  includeInactive: boolean
  page: number
}

export function ClassesPage() {
  const queryClient = useQueryClient()
  const location = useLocation()
  const [filters, setFilters] = useState<Filters>({
    search: '', level: '', day: '', sortBy: 'day', desc: false, includeInactive: true, page: 1,
  })
  // A message passed from the form page after a successful save.
  const [message, setMessage] = useState<string | null>((location.state as { message?: string } | null)?.message ?? null)

  // The filters are part of the query key, so changing any filter fetches (and caches) that result.
  const { data, isPending, error, isFetching } = useQuery({
    queryKey: ['classes', filters],
    queryFn: () => api<PagedResult<ChessClass>>(`/api/classes${toQuery({ ...filters, pageSize: 10 })}`),
    placeholderData: keepPreviousData, // keep the old page on screen while the next one loads
  })

  const remove = useMutation({
    mutationFn: (id: number) => api<void>(`/api/classes/${id}`, { method: 'DELETE' }),
    onSuccess: () => {
      setMessage('Class deleted.')
      queryClient.invalidateQueries({ queryKey: ['classes'] })
    },
  })

  // Any filter change goes back to page 1.
  const update = (change: Partial<Filters>) => setFilters((f) => ({ ...f, page: 1, ...change }))

  const handleDelete = (klass: ChessClass) => {
    setMessage(null)
    if (window.confirm(`Delete "${klass.name}"? This cannot be undone.`)) remove.mutate(klass.id)
  }

  return (
    <section aria-labelledby="classes-title">
      <div className="page-head">
        <h1 id="classes-title">Classes</h1>
        <Link to="/admin/classes/new" className="btn-primary">+ New class</Link>
      </div>

      {message && <SuccessAlert message={message} />}
      {remove.error && <ErrorAlert error={remove.error} title="Could not delete" />}

      <form className="filters" onSubmit={(e) => e.preventDefault()} role="search" aria-label="Filter classes">
        <label>
          Search
          <input type="search" placeholder="Class or coach name" value={filters.search} onChange={(e) => update({ search: e.target.value })} />
        </label>
        <label>
          Level
          <select value={filters.level} onChange={(e) => update({ level: e.target.value })}>
            <option value="">All levels</option>
            {LEVELS.map((l) => <option key={l}>{l}</option>)}
          </select>
        </label>
        <label>
          Day
          <select value={filters.day} onChange={(e) => update({ day: e.target.value })}>
            <option value="">All days</option>
            {DAYS.map((d) => <option key={d}>{d}</option>)}
          </select>
        </label>
        <label>
          Sort by
          <select value={filters.sortBy} onChange={(e) => update({ sortBy: e.target.value })}>
            <option value="day">Day & time</option>
            <option value="name">Name</option>
            <option value="fee">Fee</option>
            <option value="seatsLeft">Seats left</option>
          </select>
        </label>
        <label className="checkbox">
          <input type="checkbox" checked={filters.desc} onChange={(e) => update({ desc: e.target.checked })} />
          Descending
        </label>
        <label className="checkbox">
          <input type="checkbox" checked={filters.includeInactive} onChange={(e) => update({ includeInactive: e.target.checked })} />
          Show inactive
        </label>
      </form>

      {isPending ? (
        <Loading label="Loading classes…" />
      ) : error ? (
        <ErrorAlert error={error} title="Could not load classes" />
      ) : data.items.length === 0 ? (
        <Empty message="No classes match these filters." />
      ) : (
        <>
          <div className="table-wrap" aria-busy={isFetching}>
            <table className="table">
              <caption className="sr-only">Chess classes</caption>
              <thead>
                <tr>
                  <th scope="col">Name</th>
                  <th scope="col">Level</th>
                  <th scope="col">When</th>
                  <th scope="col">Coach</th>
                  <th scope="col" className="num">Seats</th>
                  <th scope="col" className="num">Monthly fee</th>
                  <th scope="col">Status</th>
                  <th scope="col"><span className="sr-only">Actions</span></th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((c) => (
                  <tr key={c.id} className={c.isActive ? '' : 'row-muted'}>
                    <td>{c.name}</td>
                    <td>{c.level}</td>
                    <td>{c.dayOfWeek} {hhmm(c.startTime)}–{hhmm(c.endTime)}</td>
                    <td>{c.coachName}</td>
                    <td className={`num${c.seatsLeft === 0 ? ' text-bad' : c.seatsLeft === 1 ? ' text-warn' : ''}`}>
                      {c.seatsTaken}/{c.capacity}
                    </td>
                    <td className="num">{lkr(c.monthlyFee)}</td>
                    <td>{c.isActive ? 'Active' : 'Inactive'}</td>
                    <td className="actions">
                      <Link to={`/admin/classes/${c.id}/edit`} aria-label={`Edit ${c.name}`}>Edit</Link>
                      <button type="button" className="btn-link danger" onClick={() => handleDelete(c)} aria-label={`Delete ${c.name}`}>
                        Delete
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pagination page={data.page} totalPages={data.totalPages} totalCount={data.totalCount} onChange={(page) => setFilters((f) => ({ ...f, page }))} />
        </>
      )}
    </section>
  )
}
