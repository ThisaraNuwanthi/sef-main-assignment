interface Props {
  page: number
  totalPages: number
  totalCount: number
  onChange: (page: number) => void
}

export function Pagination({ page, totalPages, totalCount, onChange }: Props) {
  if (totalCount === 0) return null
  return (
    <nav className="pagination" aria-label="Pagination">
      <button type="button" onClick={() => onChange(page - 1)} disabled={page <= 1}>
        ← Previous
      </button>
      <span>
        Page {page} of {Math.max(totalPages, 1)} · {totalCount} total
      </span>
      <button type="button" onClick={() => onChange(page + 1)} disabled={page >= totalPages}>
        Next →
      </button>
    </nav>
  )
}
