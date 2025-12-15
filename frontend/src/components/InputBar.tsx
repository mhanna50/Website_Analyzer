import type { FormEvent, KeyboardEvent } from 'react'
import { useEffect, useRef } from 'react'

type InputBarProps = {
  value: string
  onChange: (value: string) => void
  onSubmit: (value: string) => void
  placeholder?: string
  ariaLabel?: string
  isLoading?: boolean
  error?: string | null
}

const MAX_HEIGHT = 200

export default function InputBar({
  value,
  onChange,
  onSubmit,
  placeholder = 'Paste a URL or describe the audit you need...',
  ariaLabel = 'Enter a site URL or question',
  isLoading = false,
  error,
}: InputBarProps) {
  const textareaRef = useRef<HTMLTextAreaElement>(null)

  useEffect(() => {
    const textarea = textareaRef.current
    if (!textarea) return
    textarea.style.height = 'auto'
    const nextHeight = Math.min(textarea.scrollHeight, MAX_HEIGHT)
    textarea.style.height = `${nextHeight}px`
    textarea.style.overflowY = textarea.scrollHeight > MAX_HEIGHT ? 'auto' : 'hidden'
  }, [value])

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const trimmed = value.trim()
    if (!trimmed || isLoading) return
    onSubmit(trimmed)
  }

  const handleKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault()
      const trimmed = value.trim()
      if (!trimmed || isLoading) return
      onSubmit(trimmed)
    }
  }

  return (
    <form className="input-bar" onSubmit={handleSubmit}>
      {error && <p className="error-text">{error}</p>}
      <div className="input-card">
        <textarea
          ref={textareaRef}
          value={value}
          placeholder={placeholder}
          aria-label={ariaLabel}
          onChange={(event) => onChange(event.target.value)}
          onKeyDown={handleKeyDown}
        />
        <button type="submit" disabled={!value.trim() || isLoading}>
          {isLoading ? (
            <span className="spinner" aria-label="Loading" />
          ) : (
            <>
              <span>Send</span>
              <svg
                viewBox="0 0 24 24"
                role="presentation"
                aria-hidden="true"
              >
                <path
                  d="M5 12h14M13 5l7 7-7 7"
                  fill="none"
                  stroke="currentColor"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth="1.6"
                />
              </svg>
            </>
          )}
        </button>
      </div>
    </form>
  )
}
