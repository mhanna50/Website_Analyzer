
# Website Speed & SEO Performance Analyzer

A full-stack tool that audits any URL for performance, on-page SEO, accessibility, and off-page signals, then returns an AI-generated checklist so you know exactly what to fix. I built this to streamline agency-style audits: paste a URL, wait a few seconds, and hand the client a downloadable PDF plus a prioritized to-do list.

---

##  Highlights

- **Network checks** – reachability, SSL/HTTPS, redirects, HTTP status, response time.
- **Core Web Vitals** – LCP, FCP, CLS, TBT via Google PageSpeed (mobile + desktop strategies).
- **On-page SEO** – title/meta/canonical quality, headings, viewport meta, indexability, robots directives, internal/external link counts, image alt coverage (even on SPAs thanks to Playwright DOM rendering).
- **Accessibility heuristics** – missing `lang`, skip links, landmarks, unlabeled form controls.
- **Structured data & social tags** – JSON-LD counts, Open Graph, Twitter cards.
- **Off-page SEO (optional)** – domain authority, backlinks, referring domains (pluggable provider).
- **AI optimization checklist** – OpenAI summarizes the scan into actionable, checkbox-ready steps.
- **Downloadable report** – Playwright renders a polished PDF summarizing every card.
- **History sidebar** – jump back to any previous scan and compare scores.

---

##  Architecture Overview

| Layer | Tools | Responsibilities |
| ----- | ----- | ---------------- |
| **Frontend** | React + Vite + TypeScript | URL input, loading states, chat-style analyzer status, report cards, AI checklist, collapsible history. |
| **Backend** | ASP.NET Core Minimal API (.NET 8) | Endpoint orchestration, network/SEO analyzers, scoring engine, history persistence, PDF rendering. |
| **Parsing/Rendering** | HtmlAgilityPack, Playwright | Parses static HTML, renders client-side DOMs, captures screenshots/PDF. |
| **External APIs** | Google PageSpeed, OpenAI Chat Completions, optional SEO providers | Supplies Core Web Vitals, generative checklist, and off-page metrics. |
| **Secrets** | `.env` + `LoadDotEnv()` | All API keys live outside source control; `appsettings*.json` contains placeholders only. |

### Backend flow (`POST /api/analyze`)

```mermaid
flowchart TD
    A[User submits URL] --> B(Validate & normalize)
    B --> C(Network analyzer)
    B --> D(PageSpeed service)
    B --> E(SEO analyzer + Playwright DOM)
    B --> F(Optional Off-page service)
    C & D & E & F --> G(Score engine + history store)
    G --> H(AI checklist prompt)
    H --> I(Return AnalysisResult JSON)
```

### Scoring

- **Performance score** uses weighted metrics:
  - LCP 35% · FCP 15% · TBT 20% · CLS 15% · Server response 15%
- **SEO score** weights:
  - Indexability (25%), metadata quality (20%), technical basics (15%), content structure (20%), accessibility (15%), social tags (5%)
- **Overall score** = 60% performance + 40% SEO, labeled as “Excellent”, “Needs work”, or “Critical.”

---

##  Frontend Experience

- **Empty state** – vertically centered hero text + multi-line URL input.
- **Chat pane** – analyzer messages only (the “You” bubble is suppressed once a scan starts).
- **Score cards** – smooth view transitions, no awkward word-breaks (“Present” stays intact).
- **Detail grid** – network, headings, accessibility, structured data, off-page.
- **AI Checklist** – each bullet becomes a checkbox so you can track progress live.
- **History panel** – collapsible desktop drawer + mobile overlay.
- **Download PDF** – fetches `/api/report/pdf` to hand clients a shareable artifact.

---

##  Secrets & Config

1. Copy `.env.example` → `.env` and fill in:
   ```env
   OPENAI_API_KEY=sk-...
   OPENAI_MODEL=gpt-4o-mini
   OPENAI_API_BASE_URL=https://api.openai.com/v1/chat/completions
   PERFORMANCE_API_KEY=AIza...
   PERFORMANCE_API_BASE_URL=https://www.googleapis.com/pagespeedonline/v5/runPagespeed
   ```
2. `.env` is gitignored; `LoadDotEnv()` walks up directories so the API finds it even when running from `SiteMonitor/SiteMonitor.Api/`.
3. `appsettings*.json` contains empty placeholders, keeping secrets out of commits.

---

##  Getting Started

```bash
git clone https://github.com/mhanna50/Website_Speed_Analyzer.git
cd Website_Speed_Analyzer

# Backend
cd SiteMonitor/SiteMonitor.Api
dotnet run

# Frontend
cd ../frontend
npm install
npm run dev
```

Open the frontend (e.g., http://localhost:5173), paste a URL, click “Analyze,” and watch the dashboard populate. The backend logs each external call so you can trace errors (e.g., PageSpeed quotas, OpenAI latency).

---

##  Development Notes

- **HTTPS redirection** is enabled; in dev you may see “Failed to determine the https port” warnings if the launch profile only specifies HTTP—harmless, but set `ASPNETCORE_HTTPS_PORT` to silence it.
- **Playwright** bundles platform-specific binaries; they stay out of Git thanks to `.gitignore`.
- **HistoryStore** currently uses a simple JSON file for demo purposes; swap in Redis/DB for multi-user scenarios.
- **AI prompt** enforces that every checklist bullet references the site and includes actionable steps, cleaning up any leftover Markdown checkboxes before rendering in React.

---

##  Contributing / Roadmap

- Plug in additional performance providers (WebPageTest, Calibre, etc.)
- Add Lighthouse accessibility audits
- Multi-language AI prompts
- Scheduled scans + email summaries

PRs welcome—just keep secrets out of commits and follow the scoring structure. Happy auditing! 
