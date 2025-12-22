import { useState, type Dispatch, type ReactNode, type SetStateAction } from 'react'
import type { AnalysisResult } from '../api/analyzerApi'

type ReportViewProps = {
  sessionId: string
  analysis: AnalysisResult
  checkedItems: Record<string, boolean>
  onToggleChecklist: (itemId: string) => void
}

export default function ReportView({
  sessionId,
  analysis,
  checkedItems,
  onToggleChecklist,
}: ReportViewProps) {
  const perfScore = analysis.score.speed
  const seoScore = analysis.score.seo
  const performance = analysis.performance
  const mobilePerformance = performance?.mobile ?? null
  const desktopPerformance = performance?.desktop ?? null
  const schemaSummary = formatSchemaTypes(analysis.seo.structuredDataTypes)
  const aiSections = parseAiInsights(analysis.aiInsights?.recommendations ?? '')
  const [openSections, setOpenSections] = useState<Record<string, boolean>>({})
  const infoCopy = {
    network:
      'URL + HTTP status confirm uptime, response time shows latency, and the error row surfaces TLS/server failures.',
    headings:
      'H1/H2 counts validate content hierarchy, image alt coverage shows accessibility, and link mix highlights crawl paths.',
    accessibility:
      'Language attribute, skip links, landmarks, and labeled inputs indicate whether assistive technologies can navigate the page.',
    structured:
      'Structured data powers search rich results, Open Graph drives generic social previews, and Twitter cards are metadata Twitter/X uses to show summary cards.',
  }

  const networkFixes: string[] = []
  if (analysis.network.statusCode === 0 || analysis.network.statusCode >= 400) {
    networkFixes.push('Resolve the HTTP errors so crawlers receive a successful 200 response.')
  }
  const isSlowResponse = analysis.network.responseTimeMs > 1000
  if (isSlowResponse) {
    const formatted = formatMilliseconds(analysis.network.responseTimeMs)
    networkFixes.push(
      `Response time is ${formatted}. Cache the HTML at the edge or push heavy database work off the request path so TTFB stays under 1 second.`,
    )
  }
  if (analysis.network.errorMessage) {
    networkFixes.push('Investigate the reported network error in your logs and ensure TLS/certs are valid.')
  }

  const headingFixes: string[] = []
  if (analysis.seo.h1Count === 0) {
    headingFixes.push('Add a single descriptive <h1> that states the primary topic of the page.')
  } else if (analysis.seo.h1Count > 1) {
    headingFixes.push('Use only one <h1>; demote supporting headings to <h2>/<h3>.')
  }
  if (analysis.seo.h2Count === 0) {
    headingFixes.push('Introduce section-level <h2> headings so crawlers understand page hierarchy.')
  }
  if (analysis.seo.imagesWithoutAlt > 0) {
    headingFixes.push('Supply alt text for each decorative/product image so screen readers understand the content.')
  }

  const accessibilityFixes: string[] = []
  if (!analysis.seo.hasLanguageAttribute) {
    accessibilityFixes.push('Set lang="en" (or actual locale) on the <html> element so assistive tech picks the right voice.')
  }
  if (!analysis.seo.hasSkipLink) {
    accessibilityFixes.push('Add a "Skip to content" anchor so keyboard users can bypass nav quickly.')
  }
  if (analysis.seo.formControlsWithoutLabels > 0) {
    accessibilityFixes.push('Label every input with <label for=""> or aria-label to describe its purpose.')
  }
  if (analysis.seo.landmarkCount === 0) {
    accessibilityFixes.push('Add semantic landmarks (<main>, <nav>, etc.) so screen readers can jump to sections.')
  }

  const structuredFixes: string[] = []
  if (analysis.seo.structuredDataCount === 0) {
    structuredFixes.push('Add schema.org JSON-LD for your page type (Article, Product, etc.) to unlock rich results.')
  }
  if (!analysis.seo.hasOpenGraphTags) {
    structuredFixes.push('Define og:title/description/image so shared links render a branded preview.')
  }
  if (!analysis.seo.hasTwitterCard) {
    structuredFixes.push('Add twitter:card metadata so Twitter/X shows summary cards when links are shared.')
  }

  return (
    <section className="report-view report-appear">
      <div className="score-grid">
        <div className="score-card">
          <header>
            <div>
              <p>Performance score</p>
              <strong>{formatScore(perfScore)}</strong>
            </div>
            <span className={scoreBadgeClass(perfScore)}>{scoreMeaning(perfScore)}</span>
          </header>
          <p className="score-description">
            Speed is derived from Core Web Vitals and blocking-time metrics. Lower times mean a more
            reactive, smoother experience.
          </p>
          <dl>
            <Metric
              label="Mobile score"
              value={
                typeof mobilePerformance?.score === 'number'
                  ? formatMetricValue(formatScore(mobilePerformance.score), mobilePerformance.score >= 85)
                  : 'N/A'
              }
            />
            <Metric
              label="Desktop score"
              value={
                typeof desktopPerformance?.score === 'number'
                  ? formatMetricValue(formatScore(desktopPerformance.score), desktopPerformance.score >= 85)
                  : 'N/A'
              }
            />
            <Metric
              label="Largest Contentful Paint"
              value={
                <MetricComparison
                  mobile={mobilePerformance && formatMilliseconds(mobilePerformance.largestContentfulPaintMs)}
                  desktop={desktopPerformance && formatMilliseconds(desktopPerformance.largestContentfulPaintMs)}
                />
              }
            />
            <Metric
              label="First Contentful Paint"
              value={
                <MetricComparison
                  mobile={mobilePerformance && formatMilliseconds(mobilePerformance.firstContentfulPaintMs)}
                  desktop={desktopPerformance && formatMilliseconds(desktopPerformance.firstContentfulPaintMs)}
                />
              }
            />
            <Metric
              label="Cumulative Layout Shift"
              value={
                <MetricComparison
                  mobile={mobilePerformance && formatNumber(mobilePerformance.cumulativeLayoutShift)}
                  desktop={desktopPerformance && formatNumber(desktopPerformance.cumulativeLayoutShift)}
                />
              }
            />
            <Metric
              label="Total Blocking Time"
              value={
                <MetricComparison
                  mobile={mobilePerformance && formatMilliseconds(mobilePerformance.totalBlockingTimeMs)}
                  desktop={desktopPerformance && formatMilliseconds(desktopPerformance.totalBlockingTimeMs)}
                />
              }
            />
          </dl>
          <div className="list-card">
            <p className="list-title">What this means:</p>
            <ul>
              <li>LCP and FCP reveal how quickly above-the-fold content paints.</li>
              <li>TBT highlights JavaScript that blocks user input from firing.</li>
              <li>Stable CLS prevents layout jumps that frustrate visitors.</li>
            </ul>
          </div>
        </div>
        <div className="score-card">
          <header>
            <div>
              <p>SEO score</p>
              <strong>{formatScore(seoScore)}</strong>
            </div>
            <span className={scoreBadgeClass(seoScore)}>{scoreMeaning(seoScore)}</span>
          </header>
          <p className="score-description">
            SEO takes meta data, crawlability, structured markup, and accessibility basics into
            account.
          </p>
          <dl>
            <Metric label="Title" value={analysis.seo.title || 'Missing'} />
            <Metric label="Meta description" value={analysis.seo.metaDescription ? 'Found' : 'Missing'} />
            <Metric label="Indexable" value={analysis.seo.isIndexable ? 'Yes' : 'No'} />
            <Metric label="Viewport Meta" value={analysis.seo.hasViewportMeta ? 'Present' : 'Missing'} />
            <Metric label="HTTPS" value={analysis.seo.usesHttps ? 'Enabled' : 'Not secure'} />
          </dl>
          <div className="list-card">
            <p className="list-title">What this means:</p>
            <ul>
              <li>Ensure the title and meta description include the target keyword.</li>
              <li>Every page should expose a viewport meta tag for mobile friendliness.</li>
              <li>HTTPS and canonical tags are foundational for trust and indexing.</li>
            </ul>
          </div>
        </div>
      </div>

      <div className="detail-grid">
        <article>
          <div className="section-heading">
            <h3>Network</h3>
            <InfoIcon tooltip={infoCopy.network} />
          </div>
          <dl>
            <Metric label="URL" value={analysis.network.url} isUrl />
            <Metric
              label="Status Code"
              value={formatMetricValue(
                analysis.network.statusCode || 'N/A',
                analysis.network.statusCode >= 200 && analysis.network.statusCode < 400,
              )}
            />
            <Metric
              label="Response Time"
              value={formatMetricValue(`${analysis.network.responseTimeMs} ms`, analysis.network.responseTimeMs <= 1000)}
            />
            <Metric
              label="Redirects"
              value={formatMetricValue(
                analysis.network.redirectCount,
                analysis.network.redirectCount === 0,
              )}
            />
            {analysis.network.errorMessage && (
              <Metric label="Error" value={formatMetricValue(analysis.network.errorMessage, false)} />
            )}
          </dl>
          {renderSectionFixes('network', networkFixes, openSections, setOpenSections)}
        </article>
        <article>
          <div className="section-heading">
            <h3>Headings</h3>
            <InfoIcon tooltip={infoCopy.headings} />
          </div>
          <dl>
            <Metric
              label="H1 count"
              value={formatMetricValue(analysis.seo.h1Count, analysis.seo.h1Count > 0)}
            />
            <Metric
              label="H2 count"
              value={formatMetricValue(analysis.seo.h2Count, analysis.seo.h2Count > 0)}
            />
            <Metric
              label="Images missing alt"
              value={formatMetricValue(
                `${analysis.seo.imagesWithoutAlt}/${analysis.seo.totalImages}`,
                analysis.seo.imagesWithoutAlt === 0,
              )}
            />
            <Metric
              label="Internal / external links"
              value={`${analysis.seo.internalLinkCount}/${analysis.seo.externalLinkCount}`}
            />
          </dl>
          {renderSectionFixes('headings', headingFixes, openSections, setOpenSections)}
        </article>
        <article>
          <div className="section-heading">
            <h3>Accessibility</h3>
            <InfoIcon tooltip={infoCopy.accessibility} />
          </div>
          <dl>
            <Metric
              label="Language attribute"
              value={formatMetricValue(
                analysis.seo.hasLanguageAttribute ? 'Present' : 'Missing',
                analysis.seo.hasLanguageAttribute,
              )}
            />
            <Metric
              label="Skip link"
              value={formatMetricValue(
                analysis.seo.hasSkipLink ? 'Present' : 'Missing',
                analysis.seo.hasSkipLink,
              )}
            />
            <Metric
              label="Landmarks"
              value={formatMetricValue(
                analysis.seo.landmarkCount,
                analysis.seo.landmarkCount > 0,
              )}
            />
            <Metric
              label="Inputs without labels"
              value={formatMetricValue(
                analysis.seo.formControlsWithoutLabels,
                analysis.seo.formControlsWithoutLabels === 0,
              )}
            />
          </dl>
          {renderSectionFixes('accessibility', accessibilityFixes, openSections, setOpenSections)}
        </article>
        <article>
          <div className="section-heading">
            <h3>Structured data &amp; social</h3>
            <InfoIcon tooltip={infoCopy.structured} />
          </div>
          <dl>
            <Metric
              label="Structured data blocks"
              value={formatMetricValue(
                analysis.seo.structuredDataCount,
                analysis.seo.structuredDataCount > 0,
              )}
            />
            <Metric
              label="Schema types"
              value={formatMetricValue(schemaSummary, analysis.seo.structuredDataTypes.length > 0)}
            />
            <Metric
              label="Open Graph"
              value={formatMetricValue(
                analysis.seo.hasOpenGraphTags ? 'Present' : 'Missing',
                analysis.seo.hasOpenGraphTags,
              )}
            />
            <Metric
              label="Twitter card"
              value={formatMetricValue(
                analysis.seo.hasTwitterCard ? 'Present' : 'Missing',
                analysis.seo.hasTwitterCard,
              )}
            />
          </dl>
          {renderSectionFixes('structured', structuredFixes, openSections, setOpenSections)}
        </article>
        {analysis.offPageSeo && (
          <article>
            <h3>Off-page SEO</h3>
            <dl>
              <Metric label="Domain authority" value={formatScore(analysis.offPageSeo.domainAuthority)} />
              <Metric label="Backlinks" value={analysis.offPageSeo.backlinks ?? 'N/A'} />
              <Metric label="Referring domains" value={analysis.offPageSeo.referringDomains ?? 'N/A'} />
              <Metric label="Spam score" value={formatScore(analysis.offPageSeo.spamScore)} />
            </dl>
          </article>
        )}
      </div>
      {analysis.aiInsights && aiSections.length > 0 && (
        <article className="ai-card">
          <div className="ai-card-header">
            <h3>Optimization checklist</h3>
          </div>
          <div className="ai-card-body">
            <div className="ai-sections">
              {aiSections.map((section, sectionIndex) => {
                const sectionKey = slugify(section.title || `section-${sectionIndex}`)
                return (
                  <div className="ai-section" key={`${sessionId}-${sectionKey}`}>
                    {section.title && <p className="ai-section-title">{section.title}</p>}
                    <ul className="ai-checklist">
                      {section.items.map((item, itemIndex) => {
                        const itemKey = `${sectionKey}-${itemIndex}`
                        const checked = Boolean(checkedItems[itemKey])
                        return (
                          <li key={itemKey}>
                            <label>
                              <input
                                type="checkbox"
                                checked={checked}
                                onChange={() => onToggleChecklist(itemKey)}
                              />
                              <span>{item}</span>
                            </label>
                          </li>
                        )
                      })}
                    </ul>
                  </div>
                )
              })}
            </div>
          </div>
        </article>
      )}
    </section>
  )
}

function Metric({
  label,
  value,
  isUrl = false,
}: {
  label: string
  value: ReactNode
  isUrl?: boolean
}) {
  return (
    <div className={`metric-row${isUrl ? ' metric-row--url' : ''}`}>
      <dt>{label}</dt>
      <dd>{value ?? 'N/A'}</dd>
    </div>
  )
}

function MetricComparison({ mobile, desktop }: { mobile?: ReactNode; desktop?: ReactNode }) {
  if (!mobile && !desktop) {
    return 'N/A'
  }

  return (
    <div className="metric-comparison">
      {mobile && (
        <span>
          <strong>M</strong>
          {mobile}
        </span>
      )}
      {desktop && (
        <span>
          <strong>D</strong>
          {desktop}
        </span>
      )}
    </div>
  )
}

function formatScore(value?: number | null) {
  if (typeof value !== 'number' || Number.isNaN(value)) {
    return 'N/A'
  }
  return Math.round(value)
}

function scoreMeaning(value?: number | null) {
  if (typeof value !== 'number') return 'Unknown'
  if (value >= 85) return 'Excellent'
  if (value >= 65) return 'Needs work'
  return 'Critical'
}

function scoreBadgeClass(value?: number | null) {
  if (typeof value !== 'number') return 'badge badge-unknown'
  if (value >= 85) return 'badge badge-good'
  if (value >= 65) return 'badge badge-warn'
  return 'badge badge-bad'
}

function formatMilliseconds(value?: number | null) {
  if (typeof value !== 'number' || Number.isNaN(value)) return 'N/A'
  if (value >= 1000) {
    return `${(value / 1000).toFixed(1)}s`
  }
  return `${Math.round(value)}ms`
}

function formatNumber(value?: number | null) {
  if (typeof value !== 'number' || Number.isNaN(value)) return 'N/A'
  return value.toFixed(2)
}

function formatSchemaTypes(types: string[]) {
  if (!types || types.length === 0) {
    return 'None detected'
  }
  return types.slice(0, 4).join(', ')
}

type InsightSection = {
  title: string
  items: string[]
}

function parseAiInsights(raw: string): InsightSection[] {
  if (!raw.trim()) {
    return []
  }

  const buckets: Record<'Performance' | 'SEO', string[]> = {
    Performance: [],
    SEO: [],
  }
  let current: keyof typeof buckets = 'Performance'

  raw.split(/\r?\n/).forEach((line) => {
    const trimmed = line.trim()
    if (!trimmed) {
      return
    }

    const markdownHeading = trimmed.match(/^#{1,6}\s*(.+)$/)
    const explicitHeading = trimmed.match(/^(.+?)[：:]\s*$/)

    if (markdownHeading) {
      current = resolveSection(markdownHeading[1])
      return
    }

    if (explicitHeading && explicitHeading[1].length > 1) {
      current = resolveSection(explicitHeading[1])
      return
    }

    const bulletMatch =
      trimmed.match(/^[-*•]\s+(.*)$/) ??
      trimmed.match(/^\d+\.\s+(.*)$/) ??
      trimmed.match(/^\[\s?[xX]?\]\s+(.*)$/)

    const normalized = bulletMatch ? bulletMatch[1].trim() : trimmed
    const sanitized = normalized.replace(/^\[\s?[xX]?\]\s*/, '').trim()
    if (sanitized) {
      buckets[current].push(sanitized)
    }
  })

  return (Object.keys(buckets) as Array<keyof typeof buckets>)
    .filter((key) => buckets[key].length > 0)
    .map((key) => ({
      title: key,
      items: buckets[key],
    }))
}

function resolveSection(heading: string): 'Performance' | 'SEO' {
  const normalized = heading.toLowerCase()
  if (normalized.includes('seo')) {
    return 'SEO'
  }
  if (normalized.includes('performance')) {
    return 'Performance'
  }
  return 'Performance'
}

function slugify(value: string) {
  return (
    value
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/(^-|-$)/g, '') || 'section'
  )
}

function formatMetricValue(value: ReactNode, isGood: boolean) {
  return <span className={`metric-status ${isGood ? 'status-good' : 'status-bad'}`}>{value}</span>
}

function renderSectionFixes(
  key: string,
  fixes: string[],
  openSections: Record<string, boolean>,
  setOpenSections: Dispatch<SetStateAction<Record<string, boolean>>>,
) {
  if (fixes.length === 0) {
    return (
      <div className="section-status optimized">
        <span>Optimized</span>
        <span className="checkmark-icon">✔</span>
      </div>
    )
  }

  const isOpen = openSections[key]
  const toggle = () =>
    setOpenSections((prev) => ({
      ...prev,
      [key]: !prev[key],
    }))

  return (
    <div className="section-status">
      <button type="button" className="section-toggle" onClick={toggle}>
        <span>Fix these</span>
        <span className={`section-arrow ${isOpen ? 'is-open' : ''}`} aria-hidden="true">
          ▾
        </span>
      </button>
      {isOpen && (
        <div className="list-card">
          <ul>
            {fixes.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
        </div>
      )}
    </div>
  )
}

function InfoIcon({ tooltip }: { tooltip: string }) {
  return (
    <span className="info-icon" aria-label={tooltip} role="note">
      i
      <span className="info-tooltip">{tooltip}</span>
    </span>
  )
}
