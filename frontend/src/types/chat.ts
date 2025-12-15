import type { AnalysisResult } from '../api/analyzerApi'

export type MessageRole = 'user' | 'analyzer'

export type Message = {
  id: string
  role: MessageRole
  content: string
}

export type Session = {
  id: string
  title: string
  createdAt: string
  analysis?: AnalysisResult | null
  messages: Message[]
}
