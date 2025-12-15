import InputBar from './InputBar'

type EmptyStateProps = {
  value: string
  onChange: (value: string) => void
  onSubmit: (query: string) => void
  isLoading: boolean
  error: string | null
  placeholder: string
}

export default function EmptyState({
  value,
  onChange,
  onSubmit,
  isLoading,
  error,
  placeholder,
}: EmptyStateProps) {
  return (
    <div className="empty-state">
      <div className="empty-copy">
        <p className="eyebrow">Website performance &amp; SEO analyzer</p>
        <h1 className="headline">A Deep-Dive Diagnostic Tool for Modern Websites</h1>
      </div>
      <InputBar
        value={value}
        onChange={onChange}
        onSubmit={onSubmit}
        isLoading={isLoading}
        error={error}
        placeholder={placeholder}
      />
    </div>
  )
}
