import { useEffect, useRef } from 'react'
import type { Session } from '../types/chat'
import InputBar from './InputBar'
import MessageBubble from './MessageBubble'
import ReportView from './ReportView'

type ChatViewProps = {
  session: Session
  inputValue: string
  onInputChange: (value: string) => void
  onSubmit: (query: string) => void
  isLoading: boolean
  error: string | null
  loadingMessage: string
  placeholder: string
  onDownloadReport: () => void
  isDownloadingReport: boolean
  checklist: Record<string, boolean>
  onToggleChecklist: (itemId: string) => void
}

export default function ChatView({
  session,
  inputValue,
  onInputChange,
  onSubmit,
  isLoading,
  error,
  loadingMessage,
  placeholder,
  onDownloadReport,
  isDownloadingReport,
  checklist,
  onToggleChecklist,
}: ChatViewProps) {
  const scrollRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const container = scrollRef.current
    if (!container) return
    container.scrollTo({
      top: container.scrollHeight,
      behavior: 'smooth',
    })
  }, [session.messages, isLoading])

  const messagesToRender = session.messages.filter((message) => message.role !== 'user')

  return (
    <div className="chat-pane">
      <div className="chat-scroll" ref={scrollRef}>
        <div className="session-label">
          <div>
            <p>Website report</p>
            <h2>{session.title}</h2>
          </div>
          <div className="session-meta">
            <span>{new Date(session.createdAt).toLocaleString()}</span>
            {session.analysis && (
              <button
                type="button"
                className="download-report-button"
                onClick={onDownloadReport}
                disabled={isDownloadingReport}
              >
                {isDownloadingReport ? (
                  <>
                    <span className="spinner" aria-label="Preparing PDF" />
                    <span>Preparing PDF…</span>
                  </>
                ) : (
                  'Download PDF'
                )}
              </button>
            )}
          </div>
        </div>
        <div className="messages-lane">
          {messagesToRender.map((message) => (
            <MessageBubble key={message.id} role={message.role} content={message.content} />
          ))}
          {isLoading && <TypingBubble message={loadingMessage} />}
          {session.analysis && (
            <ReportView
              sessionId={session.id}
              analysis={session.analysis}
              checkedItems={checklist}
              onToggleChecklist={onToggleChecklist}
            />
          )}
        </div>
      </div>
      <div className="chat-input">
        <InputBar
          value={inputValue}
          onChange={onInputChange}
          onSubmit={onSubmit}
          isLoading={isLoading}
          error={error}
          placeholder={placeholder}
          showRerun={Boolean(session.analysis)}
          onRerun={() => {
            if (!session.analysis) return
            onSubmit(session.analysis.url)
          }}
        />
      </div>
    </div>
  )
}

function TypingBubble({ message }: { message: string }) {
  return (
    <div className="typing-bubble">
      <div className="message-avatar">SA</div>
      <div className="typing-status">
        <div className="typing-dots">
          <span />
          <span />
          <span />
        </div>
        <p>{message}</p>
      </div>
    </div>
  )
}
