import { useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { Link, useSearchParams } from 'react-router'
import { api, toQuery } from '../api/client'
import { ENROLMENT_STATUSES, type EnrolmentListItem, type PagedResult } from '../api/types'
import { Pagination } from '../components/Pagination'
import { Empty, ErrorAlert, Loading } from '../components/States'
import { StatusBadge } from '../components/StatusBadge'
import { dateTime, humanize } from '../utils/format'

export function EnrolmentsPage() {
  // The status filter lives in the URL (?status=...) so dashboard cards can link straight to it.
  const [searchParams, setSearchParams] = useSearchParams()
  const status = searchParams.get('status') ?? ''
  const [search, setSearch] = useState('')
  const [sortBy, setSortBy] = useState('created')
  const [page, setPage] = useState(1)

  const params = { status, search, sortBy, desc: true, page, pageSize: 10 }
  const { data, isPending, error, isFetching } = useQuery({
    queryKey: ['enrolments', params],
    queryFn: () => api<PagedResult<EnrolmentListItem>>(`/api/enrolments${toQuery(params)}`),
    placeholderData: keepPreviousData,
    refetchInterval: 10_000, // new requests arrive from parents all the time
  })

  const changeStatus = (value: string) => {
    setPage(1)
    setSearchParams(value ? { status: value } : {})
  }

  return (
    <section aria-labelledby="enrol-title">
      <h1 id="enrol-title">Enrolment requests</h1>

      <form className="filters" role="search" aria-label="Filter enrolments" onSubmit={(e) => e.preventDefault()}>
        <label>
          Search
          <input type="search" placeholder="Child or class name" value={search} onChange={(e) => { setSearch(e.target.value); setPage(1) }} />
        </label>
        <label>
          Status
          <select value={status} onChange={(e) => changeStatus(e.target.value)}>
            <option value="">All statuses</option>
            {ENROLMENT_STATUSES.map((s) => <option key={s} value={s}>{humanize(s)}</option>)}
          </select>
        </label>
        <label>
          Sort by
          <select value={sortBy} onChange={(e) => { setSortBy(e.target.value); setPage(1) }}>
            <option value="created">Newest first</option>
            <option value="updated">Recently updated</option>
            <option value="child">Child name</option>
            <option value="status">Status</option>
          </select>
        </label>
      </form>

      {isPending ? (
        <Loading label="Loading enrolments…" />
      ) : error ? (
        <ErrorAlert error={error} title="Could not load enrolments" />
      ) : data.items.length === 0 ? (
        <Empty message={status ? `No enrolments are ${humanize(status).toLowerCase()}.` : 'No enrolment requests yet.'} />
      ) : (
        <>
          <div className="table-wrap" aria-busy={isFetching}>
            <table className="table">
              <caption className="sr-only">Enrolment requests</caption>
              <thead>
                <tr>
                  <th scope="col">Child</th>
                  <th scope="col">Parent</th>
                  <th scope="col">Status</th>
                  <th scope="col">Class</th>
                  <th scope="col">Submitted</th>
                  <th scope="col"><span className="sr-only">Actions</span></th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((e) => (
                  <tr key={e.id}>
                    <td>{e.childName}</td>
                    <td>{e.parentName}</td>
                    <td><StatusBadge status={e.status} /></td>
                    <td>{e.assignedClassName ?? (e.requestedClassName ? `${e.requestedClassName} (requested)` : '—')}</td>
                    <td>{dateTime(e.createdAt)}</td>
                    <td className="actions">
                      {e.latestWorkflowId ? (
                        <Link to={`/admin/workflows/${e.latestWorkflowId}`} aria-label={`Review ${e.childName}'s request`}>
                          {e.status === 'PendingAdminApproval' ? 'Review →' : 'View'}
                        </Link>
                      ) : '—'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pagination page={data.page} totalPages={data.totalPages} totalCount={data.totalCount} onChange={setPage} />
        </>
      )}
    </section>
  )
}
