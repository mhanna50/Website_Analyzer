import type { ReactNode } from 'react'
import type { MessageRole } from '../types/chat'

type Props = {
  role: MessageRole
  content: string
}

type Segment =
  | { type: 'text'; value: string }
  | { type: 'code'; value: string; language?: string }

export default function MessageBubble({ role, content }: Props) {
  const isAnalyzer = role === 'analyzer'
  const segments = parseSegments(content)

  return (
    <article className={`message ${isAnalyzer ? 'message--analyzer' : 'message--user'}`}>
      <div className="message-avatar">{isAnalyzer ? 'SA' : 'You'}</div>
      <div className="message-body">
        {segments.map((segment, index) =>
          segment.type === 'code' ? (
            <pre className="message-code" key={`code-${index}`}>
              {segment.language && (
                <span className="message-code-lang">{segment.language}</span>
              )}
              <code>{segment.value}</code>
            </pre>
          ) : (
            <div className="message-text" key={`text-${index}`}>
              {renderText(segment.value)}
            </div>
          ),
        )}
      </div>
    </article>
  )
}

function parseSegments(raw: string): Segment[] {
  const pieces = raw.split(/```/g)
  return pieces
    .map<Segment | null>((piece, index) => {
      const trimmed = piece.trim()
      if (!trimmed) return null
      if (index % 2 === 1) {
        const newlineIndex = trimmed.indexOf('\n')
        if (newlineIndex === -1) {
          return { type: 'code', value: trimmed }
        }
        const language = trimmed.slice(0, newlineIndex).trim()
        const body = trimmed.slice(newlineIndex + 1).trim()
        return { type: 'code', value: body, language }
      }
      return { type: 'text', value: piece }
    })
    .filter((segment): segment is Segment => Boolean(segment))
}

function renderText(value: string): ReactNode {
  const lines = value.split('\n')
  const nodes: ReactNode[] = []
  let listBuffer: string[] = []
  let paragraphIndex = 0
  let listIndex = 0

  const flushList = () => {
    if (listBuffer.length === 0) return
    nodes.push(
      <ul className="message-list" key={`list-${listIndex++}`}>
        {listBuffer.map((item, idx) => (
          <li key={`li-${idx}`}>{item}</li>
        ))}
      </ul>,
    )
    listBuffer = []
  }

  lines.forEach((line) => {
    const trimmed = line.trim()
    if (!trimmed) {
      flushList()
      return
    }

    if (trimmed.startsWith('- ')) {
      listBuffer.push(trimmed.replace(/^- /, '').trim())
      return
    }

    flushList()
    nodes.push(
      <p className="message-paragraph" key={`p-${paragraphIndex++}`}>
        {trimmed}
      </p>,
    )
  })

  flushList()
  return nodes
}
