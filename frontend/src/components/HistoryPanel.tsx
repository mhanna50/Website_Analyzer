import type { Session } from '../types/chat'

type HistoryPanelProps = {
  sessions: Session[]
  selectedSessionId: string | null
  onSelectSession: (id: string) => void
  variant: 'desktop' | 'drawer'
  isOpen?: boolean
  onClose?: () => void
  isCollapsed?: boolean
  onToggleCollapse?: () => void
}

export default function HistoryPanel({
  sessions,
  selectedSessionId,
  onSelectSession,
  variant,
  isOpen = false,
  onClose,
  isCollapsed = false,
  onToggleCollapse,
}: HistoryPanelProps) {
  const isDesktop = variant === 'desktop'
  const collapsed = isDesktop && isCollapsed
  const className = [
    'history-panel',
    isDesktop ? 'history-panel--desktop' : 'history-panel--drawer',
    isOpen ? 'is-open' : '',
    collapsed ? 'is-collapsed' : '',
  ]
    .filter(Boolean)
    .join(' ')

  if (collapsed) {
    return (
      <aside className={className}>
        <button
          type="button"
          className="history-expand-button"
          onClick={() => onToggleCollapse?.()}
          aria-label="Expand history"
        >
          <svg viewBox="0 0 24 24" aria-hidden="true">
            <path
              d="M4 12h16M12 4l8 8-8 8"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
        </button>
      </aside>
    )
  }

  return (
    <aside className={className}>
      {variant === 'drawer' && (
        <div className="history-drawer-header">
          <p>Previous searches</p>
          <button type="button" onClick={() => onClose?.()}>
            Close
          </button>
        </div>
      )}
      <div className="history-heading">
        <div>
          <p>Search history</p>
          <small>Jump back into any site report.</small>
        </div>
        {variant === 'desktop' && (
          <button
            type="button"
            className="history-collapse-button"
            onClick={() => onToggleCollapse?.()}
            aria-label="Collapse history"
          >
            <svg viewBox="0 0 24 24" aria-hidden="true">
              <path
                d="M20 12H4m8 8-8-8 8-8"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          </button>
        )}
      </div>
      <div className="history-items">
        {sessions.length === 0 && <p className="history-empty">No reports yet.</p>}
        {sessions.map((session) => {
          const isActive = session.id === selectedSessionId
          return (
            <button
              key={session.id}
              type="button"
              className={`history-item ${isActive ? 'is-active' : ''}`}
              onClick={() => onSelectSession(session.id)}
            >
              <span className="history-title">{session.title}</span>
              <span className="history-meta">
                {new Date(session.createdAt).toLocaleDateString(undefined, {
                  month: 'short',
                  day: 'numeric',
                })}
              </span>
            </button>
          )
        })}
      </div>
    </aside>
  )
}
