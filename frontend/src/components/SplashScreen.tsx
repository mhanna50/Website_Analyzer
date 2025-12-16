type SplashScreenProps = {
  onContinue: () => void
}

export default function SplashScreen({ onContinue }: SplashScreenProps) {
  return (
    <div className="splash-overlay">
      <div className="splash-card">
        <div>
          <p className="splash-eyebrow">Welcome to Site Analyzer</p>
          <h1 className="splash-title">Deep audits to diagnose your site</h1>
          <p className="splash-subtitle">
            Queue a quick sanity check and a full Playwright + Core Web Vitals crawl. Every response
            couples AI guidance with the raw metrics that prove why a fix matters.
          </p>
        </div>

        <div className="splash-text">
          <p>
            <strong>What it does.</strong> Renders the DOM (even SPA routes), runs PageSpeed, checks
            structured data and social tags, and feeds the results into an AI checklist that becomes
            an interactive to-do list.
          </p>
          <p>
            <strong>How to use it.</strong> Paste a URL and press enter to perform a deep search. Let the queue
            handle longer jobs, then open the cards to see red/green statuses, fix lists, and PDF or
            checklist exports.
          </p>
          <p>
            <strong>Why it matters.</strong> Clients and site owners want proof their tech works how it should, how to fix it, and what to fix. Every card explains
            what the fields mean, shows the current health, and lists exactly how to fix problems.
          </p>
          <p>
            <strong>Why I built it.</strong> To showcase full-stack depth: Playwright crawling,
            throttled analyzers, async queues, automated tests, and front-end polish in a single,
            transparent audit tool.
          </p>
        </div>

        <button type="button" className="splash-cta" onClick={onContinue}>
          Enter the analyzer
        </button>
      </div>
    </div>
  )
}
