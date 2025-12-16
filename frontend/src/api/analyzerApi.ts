const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5218'

export type NetworkResult = {
  url: string
  statusCode: number
  responseTimeMs: number
  checkedAtUtc: string
  errorMessage?: string | null
  redirectCount: number
}

export type SeoResult = {
  title: string
  titleLength: number
  metaDescription: string
  metaDescriptionLength: number
  canonicalUrl: string
  isIndexable: boolean
  h1Count: number
  h2Count: number
  hasViewportMeta: boolean
  viewportContent: string
  totalImages: number
  imagesWithoutAlt: number
  internalLinkCount: number
  externalLinkCount: number
  usesHttps: boolean
  domFromHeadlessBrowser: boolean
  hasLanguageAttribute: boolean
  hasSkipLink: boolean
  landmarkCount: number
  formControlsWithoutLabels: number
  structuredDataCount: number
  structuredDataTypes: string[]
  hasOpenGraphTags: boolean
  hasTwitterCard: boolean
}

export type ScoreResult = {
  overall: number
  seo: number
  speed: number
}

export type PerformanceChannel = {
  strategy: 'mobile' | 'desktop' | string
  score?: number | null
  largestContentfulPaintMs?: number | null
  firstContentfulPaintMs?: number | null
  cumulativeLayoutShift?: number | null
  totalBlockingTimeMs?: number | null
}

export type PerformanceResult = {
  mobile?: PerformanceChannel | null
  desktop?: PerformanceChannel | null
  suggestions?: PerformanceSuggestion[]
}

export type PerformanceSuggestion = {
  title: string
  description?: string | null
  score?: number | null
  estimatedSavingsMs?: number | null
}

export type OffPageSeoResult = {
  domainAuthority?: number | null
  backlinks?: number | null
  referringDomains?: number | null
  spamScore?: number | null
}

export type AiInsightsResult = {
  recommendations: string
}

export type AnalysisResult = {
  url: string
  checkedAtUtc: string
  network: NetworkResult
  seo: SeoResult
  score: ScoreResult
  performance?: PerformanceResult | null
  offPageSeo?: OffPageSeoResult | null
  aiInsights?: AiInsightsResult | null
}

export type ScanMode = 'Fast' | 'Deep'

type AnalyzeRequest = {
  url: string
  mode?: ScanMode
}

export async function analyzeWebsite(url: string): Promise<AnalysisResult> {
  const response = await fetch(`${API_BASE_URL}/api/analyze`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ url } satisfies AnalyzeRequest),
  })

  if (!response.ok) {
    const message = await extractErrorMessage(response)
    throw new Error(message)
  }

  return response.json() as Promise<AnalysisResult>
}

export type ScanRecord = {
  url: string
  timestampUtc: string
  statusCode: number
  responseTimeMs: number
  performanceScore?: number | null
  isIndexable: boolean
  usesHttps: boolean
  overallScore: number
  seoScore: number
  speedScore: number
}

export async function fetchHistory(url: string): Promise<ScanRecord[]> {
  const response = await fetch(
    `${API_BASE_URL}/api/history?url=${encodeURIComponent(url)}`,
  )

  if (!response.ok) {
    const message = await extractErrorMessage(response)
    throw new Error(message)
  }

  return response.json() as Promise<ScanRecord[]>
}

export async function downloadReportImage(url: string): Promise<Blob> {
  const response = await fetch(`${API_BASE_URL}/api/report`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ url } satisfies AnalyzeRequest),
  })

  if (!response.ok) {
    const message = await extractErrorMessage(response)
    throw new Error(message)
  }

  return response.blob()
}

export async function downloadReportPdf(url: string): Promise<Blob> {
  const response = await fetch(`${API_BASE_URL}/api/report/pdf`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ url } satisfies AnalyzeRequest),
  })

  if (!response.ok) {
    const message = await extractErrorMessage(response)
    throw new Error(message)
  }

  return response.blob()
}

async function extractErrorMessage(response: Response) {
  try {
    const data = await response.json()
    if (typeof data.message === 'string') {
      return data.message
    }
  } catch {
    // ignore and fallback to status text
  }

  return response.statusText || 'Failed to analyze website.'
}
