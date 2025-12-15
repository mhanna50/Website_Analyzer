type SplashScreenProps = {
  onContinue: () => void
}

export default function SplashScreen({ onContinue }: SplashScreenProps) {
  return (
    <div className="splash-overlay">
      <div className="splash-card">
        <div>
          <p className="splash-eyebrow">Welcome to Site Analyzer</p>
          <h1 className="splash-title">See your website like an analyst would</h1>
          <p className="splash-subtitle">
            This companion app recreates the conversational feeling of ChatGPT, but every exchange
            is focused on performance, SEO, and accessibility truth data pulled from your site.
          </p>
        </div>

        <div className="splash-text">
          <p>
            <strong>What it does.</strong> Scrapes any URL, runs Core Web Vitals, inspects HTML
            metadata, and taps external APIs for backlink and authority insights—all in one
            conversational report.
          </p>
          <p>
            <strong>How to use it.</strong> Paste a URL, press enter, and read the chat-style
            breakdown. Scroll for raw metrics, then use the checklist at the bottom to plan fixes one
            checkbox at a time.
          </p>
          <p>
            <strong>Why it matters.</strong> Fast, indexable sites convert better and rank higher.
            This tool highlights the bottlenecks that slow you down so you can prioritize fixes with
            confidence.
          </p>
          <p>
            <strong>Why I built it.</strong> I wanted a transparent alternative to black-box SEO
            audits—something that feels like ChatGPT but grounds every recommendation in measurable
            performance and SEO data.
          </p>
        </div>

        <button type="button" className="splash-cta" onClick={onContinue}>
          Enter the analyzer
        </button>
      </div>
    </div>
  )
}
