import { useEffect, useState, type FormEvent, type ReactNode } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate, useParams } from 'react-router'
import { api } from '../api/client'
import { DAYS, LEVELS, type ChessClass, type CoachOption, type SaveClassRequest } from '../api/types'
import { ErrorAlert, Loading } from '../components/States'
import { hhmm, toApiTime } from '../utils/format'
import { validateClass, type ClassFormErrors as FormErrors, type ClassFormValues as FormValues } from '../utils/validation'

const EMPTY: FormValues = {
  name: '', level: 'Beginner', dayOfWeek: 'Saturday', startTime: '09:00', endTime: '10:00',
  capacity: '8', monthlyFee: '3500', coachId: '', isActive: true,
}

export function ClassFormPage() {
  const { id } = useParams()
  const isEdit = id !== undefined
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [values, setValues] = useState<FormValues>(EMPTY)
  const [errors, setErrors] = useState<FormErrors>({})

  const coaches = useQuery({ queryKey: ['coaches'], queryFn: () => api<CoachOption[]>('/api/classes/coaches') })
  const existing = useQuery({
    queryKey: ['class', id],
    queryFn: () => api<ChessClass>(`/api/classes/${id}`),
    enabled: isEdit,
  })

  // Fill the form once the class being edited has loaded.
  useEffect(() => {
    if (existing.data) {
      const c = existing.data
      setValues({
        name: c.name, level: c.level, dayOfWeek: c.dayOfWeek, startTime: hhmm(c.startTime), endTime: hhmm(c.endTime),
        capacity: String(c.capacity), monthlyFee: String(c.monthlyFee), coachId: String(c.coachId), isActive: c.isActive,
      })
    }
  }, [existing.data])

  const save = useMutation({
    mutationFn: (body: SaveClassRequest) =>
      isEdit ? api<ChessClass>(`/api/classes/${id}`, { method: 'PUT', body }) : api<ChessClass>('/api/classes', { method: 'POST', body }),
    onSuccess: (saved) => {
      queryClient.invalidateQueries({ queryKey: ['classes'] })
      navigate('/admin/classes', { state: { message: `"${saved.name}" was ${isEdit ? 'updated' : 'created'}.` } })
    },
  })

  const set = <K extends keyof FormValues>(key: K, value: FormValues[K]) => setValues((v) => ({ ...v, [key]: value }))

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault()
    const found = validateClass(values)
    setErrors(found)
    if (Object.keys(found).length > 0) return
    save.mutate({
      name: values.name.trim(),
      level: values.level as SaveClassRequest['level'],
      dayOfWeek: values.dayOfWeek as SaveClassRequest['dayOfWeek'],
      startTime: toApiTime(values.startTime),
      endTime: toApiTime(values.endTime),
      capacity: Number(values.capacity),
      monthlyFee: Number(values.monthlyFee),
      coachId: Number(values.coachId),
      isActive: values.isActive,
    })
  }

  if (isEdit && existing.isPending) return <Loading label="Loading class…" />
  if (existing.error) return <ErrorAlert error={existing.error} title="Could not load the class" />

  // Small helper so every field gets a label, error text and aria wiring the same way.
  const field = (key: keyof FormValues, label: string, input: ReactNode) => (
    <div className="field">
      <label htmlFor={key}>{label}</label>
      {input}
      {errors[key] && <p id={`${key}-error`} className="field-error">{errors[key]}</p>}
    </div>
  )
  const aria = (key: keyof FormValues) => ({
    id: key,
    'aria-invalid': !!errors[key],
    'aria-describedby': errors[key] ? `${key}-error` : undefined,
  })

  return (
    <section aria-labelledby="form-title">
      <p><Link to="/admin/classes">← Back to classes</Link></p>
      <h1 id="form-title">{isEdit ? 'Edit class' : 'New class'}</h1>

      {save.error && <ErrorAlert error={save.error} title="Could not save" />}

      <form className="card form-grid" onSubmit={handleSubmit} noValidate>
        {field('name', 'Class name', <input {...aria('name')} value={values.name} onChange={(e) => set('name', e.target.value)} />)}
        {field('level', 'Level', (
          <select {...aria('level')} value={values.level} onChange={(e) => set('level', e.target.value)}>
            {LEVELS.map((l) => <option key={l}>{l}</option>)}
          </select>
        ))}
        {field('dayOfWeek', 'Day', (
          <select {...aria('dayOfWeek')} value={values.dayOfWeek} onChange={(e) => set('dayOfWeek', e.target.value)}>
            {DAYS.map((d) => <option key={d}>{d}</option>)}
          </select>
        ))}
        {field('startTime', 'Start time', <input type="time" {...aria('startTime')} value={values.startTime} onChange={(e) => set('startTime', e.target.value)} />)}
        {field('endTime', 'End time', <input type="time" {...aria('endTime')} value={values.endTime} onChange={(e) => set('endTime', e.target.value)} />)}
        {field('capacity', 'Capacity (seats)', <input type="number" min={1} max={100} {...aria('capacity')} value={values.capacity} onChange={(e) => set('capacity', e.target.value)} />)}
        {field('monthlyFee', 'Monthly fee (LKR)', <input type="number" min={0} step="0.01" {...aria('monthlyFee')} value={values.monthlyFee} onChange={(e) => set('monthlyFee', e.target.value)} />)}
        {field('coachId', 'Coach', coaches.isPending ? <Loading label="Loading coaches…" /> : (
          <select {...aria('coachId')} value={values.coachId} onChange={(e) => set('coachId', e.target.value)}>
            <option value="">Choose a coach</option>
            {coaches.data?.map((c) => <option key={c.id} value={c.id}>{c.fullName}</option>)}
          </select>
        ))}
        <label className="checkbox">
          <input type="checkbox" checked={values.isActive} onChange={(e) => set('isActive', e.target.checked)} />
          Active (visible to parents and the placement agents)
        </label>

        <div className="form-actions">
          <button type="submit" className="btn-primary" disabled={save.isPending}>
            {save.isPending ? 'Saving…' : isEdit ? 'Save changes' : 'Create class'}
          </button>
          <Link to="/admin/classes" className="btn-secondary">Cancel</Link>
        </div>
      </form>
    </section>
  )
}
