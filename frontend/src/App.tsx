import { useEffect, useMemo, useState } from 'react'
import './App.css'
import { analyzeWebsite, downloadReportPdf, warmBackend } from './api/analyzerApi'
import ChatView from './components/ChatView'
import EmptyState from './components/EmptyState'
import HistoryPanel from './components/HistoryPanel'
import SplashScreen from './components/SplashScreen'
import type { Session } from './types/chat'

type ProgressStage = {
  label: string
  duration: number
}

const PROGRESS_STAGES: ProgressStage[] = [
  { label: 'Validating URL and SSL…', duration: 900 },
  { label: 'Fetching site HTML & headers…', duration: 1100 },
  { label: 'Rendering DOM in a headless browser…', duration: 1200 },
  { label: 'Running PageSpeed/Core Web Vitals…', duration: 1300 },
  { label: 'Collecting off-page SEO metrics…', duration: 1200 },
  { label: 'Assembling the optimization checklist…', duration: 1000 },
]

const DEFAULT_LOADING_MESSAGE = PROGRESS_STAGES[0].label

function App() {
  const [sessions, setSessions] = useState<Session[]>([])
  const [selectedSessionId, setSelectedSessionId] = useState<string | null>(null)
  const [inputValue, setInputValue] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [isHistoryOpen, setIsHistoryOpen] = useState(false)
  const [isDesktopHistoryCollapsed, setIsDesktopHistoryCollapsed] = useState(false)
  const [loadingMessage, setLoadingMessage] = useState(DEFAULT_LOADING_MESSAGE)
  const [showSplash, setShowSplash] = useState(true)
  const [checklists, setChecklists] = useState<Record<string, Record<string, boolean>>>({})
  const [downloadingSessionId, setDownloadingSessionId] = useState<string | null>(null)
  const [isViewTransitioning, setIsViewTransitioning] = useState(false)
  const [apiWarmMessage, setApiWarmMessage] = useState<string | null>(null)

  const currentSession = useMemo(() => {
    if (!selectedSessionId) {
      return null
    }
    return sessions.find((session) => session.id === selectedSessionId) ?? null
  }, [selectedSessionId, sessions])

  const showEmptyState = !currentSession
  const sessionChecklist = currentSession ? checklists[currentSession.id] ?? {} : {}

  const handleSelectSession = (sessionId: string) => {
    setSelectedSessionId(sessionId)
    setError(null)
    setIsHistoryOpen(false)
  }

  const handleNewSession = () => {
    setSelectedSessionId(null)
    setInputValue('')
    setError(null)
    setIsHistoryOpen(false)
  }

  const handleSubmit = async (query: string) => {
    const trimmed = query.trim()
    if (!trimmed || isLoading) return

    const normalizedUrl = normalizeUrl(trimmed)
    if (!normalizedUrl) {
      setError('Please enter a valid URL such as https://example.com')
      return
    }

    setIsLoading(true)
    setError(null)
    setApiWarmMessage(null)
    setInputValue('')

    const sessionId = createId()
    const userMessage = {
      id: createId(),
      role: 'user' as const,
      content: normalizedUrl,
    }

    const newSession: Session = {
      id: sessionId,
      title: deriveSessionTitle(normalizedUrl),
      createdAt: new Date().toISOString(),
      messages: [userMessage],
      analysis: null,
    }

    setSessions((prev) => [newSession, ...prev])
    setChecklists((prev) => ({ ...prev, [sessionId]: {} }))
    setSelectedSessionId(sessionId)
    setIsHistoryOpen(false)

    try {
      const result = await analyzeWebsite(normalizedUrl)
      const analyzerMessage = {
        id: createId(),
        role: 'analyzer' as const,
        content: 'Report generated. See the breakdown below.',
      }
      setSessions((prev) =>
        prev.map((session) =>
          session.id === sessionId
            ? {
                ...session,
                title: deriveSessionTitle(result.url),
                messages: [...session.messages, analyzerMessage],
                analysis: result,
              }
            : session,
        ),
      )
    } catch (err) {
      const rawMessage =
        err instanceof Error ? err.message : 'Unable to complete the analysis right now.'
      const message = formatRequestError(rawMessage)
      setError(message)
      const failureMessage = {
        id: createId(),
        role: 'analyzer' as const,
        content: `Unable to finish the analysis: ${message}`,
      }
      setSessions((prev) =>
        prev.map((session) =>
          session.id === sessionId
            ? { ...session, messages: [...session.messages, failureMessage] }
            : session,
        ),
      )
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    if (!isLoading) {
      setLoadingMessage(DEFAULT_LOADING_MESSAGE)
      return
    }

    setLoadingMessage(DEFAULT_LOADING_MESSAGE)
    const timers: number[] = []
    let accumulatedDelay = 0

    for (let index = 1; index < PROGRESS_STAGES.length; index += 1) {
      accumulatedDelay += PROGRESS_STAGES[index - 1].duration
      const timer = window.setTimeout(() => {
        setLoadingMessage(PROGRESS_STAGES[index].label)
      }, accumulatedDelay)
      timers.push(timer)
    }

    return () => {
      timers.forEach((timer) => window.clearTimeout(timer))
    }
  }, [isLoading])

  const handleToggleChecklist = (sessionId: string, itemId: string) => {
    setChecklists((prev) => {
      const sessionItems = prev[sessionId] ?? {}
      return {
        ...prev,
        [sessionId]: {
          ...sessionItems,
          [itemId]: !sessionItems[itemId],
        },
      }
    })
  }

  const handleDownloadReport = async (session: Session | null) => {
    if (!session?.analysis || downloadingSessionId === session.id) {
      return
    }
    setDownloadingSessionId(session.id)
    setError(null)

    try {
      const pdf = await downloadReportPdf(session.analysis.url)
      const fileName = buildReportFileName(session.analysis.url)
      const objectUrl = URL.createObjectURL(pdf)
      const link = document.createElement('a')
      link.href = objectUrl
      link.download = fileName
      document.body.appendChild(link)
      link.click()
      document.body.removeChild(link)
      URL.revokeObjectURL(objectUrl)
    } catch (err) {
      const rawMessage =
        err instanceof Error ? err.message : 'Unable to download the report PDF right now.'
      const message = formatRequestError(rawMessage)
      console.error(err)
      setError(message)
    } finally {
      setDownloadingSessionId(null)
    }
  }

  const placeholder = showEmptyState
    ? 'Paste a website URL (https://example.com)'
    : 'Enter another website URL (https://example.com)'

  useEffect(() => {
    let isMounted = true

    const wakeBackend = async () => {
      setApiWarmMessage('Waking the demo API… first response may take 20–30 seconds.')
      try {
        await warmBackend({ retries: 2, delayMs: 1000 })
        if (isMounted) {
          setApiWarmMessage(null)
        }
      } catch (err) {
        if (isMounted) {
          const friendly = formatRequestError(err instanceof Error ? err.message : 'Failed to reach API.')
          setApiWarmMessage(friendly)
        }
      }
    }

    void wakeBackend()
    return () => {
      isMounted = false
    }
  }, [])

  useEffect(() => {
    setIsViewTransitioning(true)
    const frame = window.requestAnimationFrame(() => {
      setIsViewTransitioning(false)
    })
    return () => window.cancelAnimationFrame(frame)
  }, [selectedSessionId, showEmptyState])

  return (
    <div className="app-shell">
      {showSplash && <SplashScreen onContinue={() => setShowSplash(false)} />}
      <header className="top-nav">
        <span className="brand">Site Analyzer</span>
        <div className="nav-actions">
          <button type="button" onClick={handleNewSession}>
            New search
          </button>
          <button type="button" className="history-toggle" onClick={() => setIsHistoryOpen(true)}>
            History
          </button>
        </div>
      </header>
      <div className="app-body">
        <main className="main-area">
          <div className="main-column">
            <div className={`view-transition ${isViewTransitioning ? 'is-transitioning' : ''}`}>
              {showEmptyState ? (
                <EmptyState
                  value={inputValue}
                  onChange={setInputValue}
                  onSubmit={handleSubmit}
                  isLoading={isLoading}
                  error={error ?? apiWarmMessage}
                  placeholder={placeholder}
                />
              ) : (
                <ChatView
                  session={currentSession}
                  inputValue={inputValue}
                  onInputChange={setInputValue}
                  onSubmit={handleSubmit}
                  isLoading={isLoading}
                  error={error ?? apiWarmMessage}
                  loadingMessage={loadingMessage}
                  placeholder={placeholder}
                  onDownloadReport={() => handleDownloadReport(currentSession)}
                  isDownloadingReport={downloadingSessionId === currentSession.id}
                  checklist={sessionChecklist}
                  onToggleChecklist={(itemId) => handleToggleChecklist(currentSession.id, itemId)}
                />
              )}
            </div>
          </div>
        </main>
        <HistoryPanel
          sessions={sessions}
          selectedSessionId={selectedSessionId}
          onSelectSession={handleSelectSession}
          variant="desktop"
          isCollapsed={isDesktopHistoryCollapsed}
          onToggleCollapse={() => setIsDesktopHistoryCollapsed((prev) => !prev)}
        />
        <HistoryPanel
          sessions={sessions}
          selectedSessionId={selectedSessionId}
          onSelectSession={handleSelectSession}
          variant="drawer"
          isOpen={isHistoryOpen}
          onClose={() => setIsHistoryOpen(false)}
        />
        {isHistoryOpen && (
          <button
            type="button"
            className="drawer-overlay"
            aria-label="Close history panel"
            onClick={() => setIsHistoryOpen(false)}
          />
        )}
      </div>
    </div>
  )
}

export default App

function buildReportFileName(url: string) {
  try {
    const parsed = new URL(url)
    const host = parsed.hostname.replace(/^www\./, '')
    return `${host}-report.pdf`
  } catch {
    return 'site-report.pdf'
  }
}

function formatRequestError(message: string) {
  const normalized = message.toLowerCase()
  if (
    normalized.includes('failed to fetch') ||
    normalized.includes('load failed') ||
    normalized.includes('network error')
  ) {
    return 'Unable to reach the demo API. It may still be waking up on the free tier—please try again shortly.'
  }
  return message
}

function deriveSessionTitle(input: string) {
  const trimmed = input.trim()
  if (!trimmed) {
    return 'New search'
  }

  try {
    const normalized = trimmed.match(/^https?:\/\//i) ? trimmed : `https://${trimmed}`
    const url = new URL(normalized)
    return url.hostname.replace(/^www\./, '')
  } catch {
    return trimmed.length > 40 ? `${trimmed.slice(0, 40).trim()}...` : trimmed
  }
}

function createId() {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID()
  }
  return Math.random().toString(36).slice(2, 10)
}

function normalizeUrl(value: string): string | null {
  const trimmed = value.trim()
  try {
    const hasProtocol = /^https?:\/\//i.test(trimmed)
    const target = hasProtocol ? trimmed : `https://${trimmed}`
    const url = new URL(target)
    if (!url.hostname) {
      return null
    }
    return url.toString()
  } catch {
    return null
  }
}
