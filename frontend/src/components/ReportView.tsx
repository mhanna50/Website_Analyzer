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
  const schemaSummary = formatSchemaTypes(analysis.seo.structuredDataTypes)
  const aiSections = parseAiInsights(analysis.aiInsights?.recommendations ?? '')

  return (
    <section className="report-view">
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
            <Metric label="Largest Contentful Paint" value={formatMilliseconds(performance?.largestContentfulPaintMs)} />
            <Metric label="First Contentful Paint" value={formatMilliseconds(performance?.firstContentfulPaintMs)} />
            <Metric
              label="Cumulative Layout Shift"
              value={formatNumber(performance?.cumulativeLayoutShift)}
            />
            <Metric label="Total Blocking Time" value={formatMilliseconds(performance?.totalBlockingTimeMs)} />
          </dl>
          {performance?.suggestions && performance.suggestions.length > 0 && (
            <div className="list-card">
              <p className="list-title">Suggestions that affect this score:</p>
              <ul>
                {performance.suggestions.slice(0, 3).map((suggestion) => (
                  <li key={suggestion.title}>{suggestion.title}</li>
                ))}
              </ul>
            </div>
          )}
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
          <h3>Network</h3>
          <dl>
            <Metric label="URL" value={analysis.network.url} />
            <Metric label="Status Code" value={analysis.network.statusCode || 'N/A'} />
            <Metric label="Response Time" value={`${analysis.network.responseTimeMs} ms`} />
            {analysis.network.errorMessage && (
              <Metric label="Error" value={analysis.network.errorMessage} />
            )}
          </dl>
        </article>
        <article>
          <h3>Headings</h3>
          <dl>
            <Metric label="H1 count" value={analysis.seo.h1Count} />
            <Metric label="H2 count" value={analysis.seo.h2Count} />
            <Metric
              label="Images missing alt"
              value={`${analysis.seo.imagesWithoutAlt}/${analysis.seo.totalImages}`}
            />
            <Metric
              label="Internal / external links"
              value={`${analysis.seo.internalLinkCount}/${analysis.seo.externalLinkCount}`}
            />
          </dl>
        </article>
        <article>
          <h3>Accessibility</h3>
          <dl>
            <Metric
              label="Language attribute"
              value={analysis.seo.hasLanguageAttribute ? 'Present' : 'Missing'}
            />
            <Metric
              label="Skip link"
              value={analysis.seo.hasSkipLink ? 'Present' : 'Missing'}
            />
            <Metric label="Landmarks" value={analysis.seo.landmarkCount} />
            <Metric
              label="Inputs without labels"
              value={analysis.seo.formControlsWithoutLabels}
            />
          </dl>
        </article>
        <article>
          <h3>Structured data &amp; social</h3>
          <dl>
            <Metric label="Structured data blocks" value={analysis.seo.structuredDataCount} />
            <Metric label="Schema types" value={schemaSummary} />
            <Metric label="Open Graph" value={analysis.seo.hasOpenGraphTags ? 'Present' : 'Missing'} />
            <Metric label="Twitter card" value={analysis.seo.hasTwitterCard ? 'Present' : 'Missing'} />
          </dl>
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
}: {
  label: string
  value: string | number | null | undefined
}) {
  return (
    <div className="metric-row">
      <dt>{label}</dt>
      <dd>{value ?? 'N/A'}</dd>
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

  const sections: InsightSection[] = []
  let current: InsightSection = { title: 'Checklist', items: [] }

  const pushSection = () => {
    if (current.items.length > 0) {
      sections.push(current)
    }
  }

  raw.split(/\r?\n/).forEach((line) => {
    const trimmed = line.trim()
    if (!trimmed) {
      return
    }

    const markdownHeading = trimmed.match(/^#{1,6}\s*(.+)$/)
    const explicitHeading = trimmed.match(/^(.+?)[：:]\s*$/)

    if (markdownHeading) {
      pushSection()
      current = { title: markdownHeading[1].trim(), items: [] }
      return
    }

    if (explicitHeading && explicitHeading[1].length > 1) {
      pushSection()
      current = { title: explicitHeading[1].trim(), items: [] }
      return
    }

    const bulletMatch =
      trimmed.match(/^[-*•]\s+(.*)$/) ??
      trimmed.match(/^\d+\.\s+(.*)$/) ??
      trimmed.match(/^\[\s?[xX]?\]\s+(.*)$/)

    const normalized = bulletMatch ? bulletMatch[1].trim() : trimmed
    const sanitized = normalized.replace(/^\[\s?[xX]?\]\s*/, '').trim()
    if (sanitized) {
      current.items.push(sanitized)
    }
  })

  pushSection()
  return sections
}

function slugify(value: string) {
  return (
    value
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/(^-|-$)/g, '') || 'section'
  )
}
