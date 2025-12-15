# Website Speed & SEO Performance Analyzer

A full-stack web application that analyzes a website’s **performance**, **technical health**, and **SEO signals** in one scan.

The app combines:
- **Raw server/network checks** (status, latency, TTFB)
- **Page performance metrics** (like Lighthouse/PageSpeed-style metrics)
- **On-page SEO analysis** (title, meta tags, headings, indexability)
- **Optional external SEO insights** (authority, backlinks, etc.)
- **Weighted scoring engine** (aggregated SEO + speed scores + recommendations)

---

## 🌐 What This App Does

Given a URL, the app:

1. **Checks basic availability & speed**
   - Is the site reachable?
   - How fast does the server respond?
   - What HTTP status code does it return?
   - How large is the response?

2. **Analyzes real page performance**
   - Uses an external performance API (PageSpeed/Lighthouse style) to gather:
     - Overall performance score (0–100)
     - Core performance metrics:
       - Largest Contentful Paint (LCP)
       - First Contentful Paint (FCP)
       - Cumulative Layout Shift (CLS)
       - Total Blocking Time (TBT) / Interaction latency
     - Separate **mobile** and **desktop** performance scores pulled via API strategies (mobile + desktop)

3. **Evaluates on-page SEO**
   - Fetches and parses the HTML to extract:
     - `<title>` and its length
     - `<meta name="description">` and its length
     - `<link rel="canonical">`
     - Basic `robots` directives (`noindex`, `nofollow`, etc.)
     - Heading structure (H1/H2 counts)
     - `<meta name="viewport">` presence & content
     - HTTPS vs HTTP usage for the requested URL
     - Image alt-text coverage (total images vs missing alts)
     - Internal vs external link counts for `<a>` tags
     - Headless-browser DOM capture (Playwright) so JS-rendered SPAs also surface image/link counts
     - Accessibility heuristics (language attribute, skip links, ARIA landmarks, unlabeled form controls)
   - Flags common issues like:
     - Missing or overly long titles/descriptions
     - Multiple H1 tags
     - No canonical tag
     - Page not indexable
     - Missing viewport meta or insecure protocol
     - Images without alt text

4. **(Optional) Pulls off-page SEO signals**
   - Integrates with external SEO APIs (e.g. Moz/Ahrefs-style tools) to show:
     - Domain authority / rating
     - Backlink count
     - Referring domains
     - Basic spam/quality scoring
   - These are treated as **optional providers** and only included if configured.

5. **Displays everything in a clean React dashboard**
   - Overview card with key metrics at a glance
   - Separate sections/tabs for:
     - **Overview**
     - **Performance**
     - **On-Page SEO**
     - **Off-Page SEO** (if available)
   - Clear “Good / Needs Improvement / Poor” visual labels.
   - Scoreboard that blends SEO + speed into a single **Overall Site Score** (0–100) with visual bars.

6. **Tracks history for recurring audits**
   - Every scan is stored with timestamp + key metrics.
   - The dashboard renders a “Recent scans” table so users can spot trends over time.
   - Each historical row records response time, HTTP status, performance score, and overall score.

7. **Generates shareable reports**
   - `POST /api/report` transforms a scan into a structured report payload with:
     - Summary (URL, time, status, consolidated scores)
     - Performance (network data + PageSpeed metrics)
     - SEO (on-page insights + DOM stats)
     - Off-page (if configured)
     - Rule-based recommendations (image alts, viewport, HTTPS, etc.)
   - The React app exposes **Download Report** (JSON) and **Copy Share Link** buttons next to the summary card.

---

## 🧱 Tech Stack

**Frontend**
- **React** (SPA dashboard)
- **TypeScript**
- Build tool: **Vite**
- Styling: CSS/Tailwind (or equivalent utility-first styling)
- HTTP client: **Axios** (or `fetch`)

**Backend**
- **ASP.NET Core (.NET)** – Web API
- C# – endpoint and analysis logic
- `HttpClient` – for network checks and external API calls

**External Services (Planned/Configurable)**
- **PageSpeed / Lighthouse-style API** for performance metrics
- Optional SEO APIs (e.g. Moz/Ahrefs-like) for off-page metrics  
- All external keys are injected via **environment variables**

---

## 🏗 High-Level Architecture

1. **React Frontend**
   - Renders a dashboard with:
     - URL input form
     - Scan button
     - Results cards and charts
   - Sends a `POST` request to the backend with the target URL.
   - Renders loading and error states while waiting for results.

2. **.NET Backend API**
   - Exposes a single main endpoint:
     - `POST /api/analyze`
   - Accepts JSON:
     ```json
     {
       "url": "https://example.com"
     }
     ```
   - Coordinates multiple “analyzers”:
     - **Network analyzer** – raw HTTP check + timing.
     - **Performance provider** – external performance API (PageSpeed-style).
     - **On-page SEO analyzer** – HTML parsing and tag inspection.
     - **Off-page SEO provider** (optional) – domain-level metrics from SEO APIs.

3. **Analysis Flow**

   1. **Validate the URL**
      - Ensure it’s non-empty and normalize it (prepend `https://` if missing).

   2. **Network / Server Check**
      - Send an HTTP GET to the URL.
      - Use a stopwatch to measure:
        - Response time (ms)
        - Time to first byte (when headers arrive), if exposed.
      - Capture:
        - Status code
        - Response size (content length if available)
        - Basic error info (timeouts, DNS errors, etc.)

   3. **Page Performance Check**
      - Call an external performance API with the URL.
      - Request both **mobile** and **desktop** results if supported.
      - Extract:
        - Overall performance score(s)
        - Key metrics like LCP, FCP, CLS, TBT/INP

   4. **On-Page SEO Check**
      - Download the HTML.
      - Parse HTML to extract:
        - Title text + length
        - Meta description + length
        - Canonical URL
        - Robots tags / indexability
        - H1/H2 counts
      - Derive “health” flags such as:
        - Title missing/too long/too short
        - Description missing/too long/too short
        - Multiple H1s or no H1
        - Non-indexable page

   5. **Off-Page SEO Check (Optional)**
      - If SEO API keys are configured:
        - Query external API with the domain.
        - Extract:
          - Domain authority/score
          - Backlinks and referring domains
          - Basic spam/quality signals

   6. **Combine Results & Save History**
      - Merge all data into a single **AnalysisResult** object:

        ```json
        {
          "url": "https://example.com",
          "checkedAtUtc": "2025-12-14T17:00:00Z",
          "network": { ... },
          "score": {
            "overall": 86,
            "seo": 78,
            "speed": 92
          },
          "performance": { ... },
          "seo": { ... },
          "offPageSeo": { ... }
        }
        ```

      - Return as JSON to the frontend.
      - Append the scan to a lightweight history store so the UI can display recent results or comparisons.

4. **Frontend Rendering**
   - The React app reads the `AnalysisResult` and displays:

     - **Overview**
     - Status (Up/Down)
     - Status code
     - Response time
     - Overall performance score
     - Recent scans table with timestamps, status, response time, and score deltas

     - **Performance Tab**
       - Key timing metrics (LCP, FCP, CLS, TBT/INP)
       - Desktop vs mobile scores
       - Simple visual indicators (“Fast”, “Moderate”, “Slow”)

     - **On-Page SEO Tab**
       - Title + length and quality indicator
       - Meta description + length and quality indicator
       - Canonical URL presence
       - Indexable vs noindex
       - H1/H2 counts

     - **Off-Page SEO Tab** (if configured)
       - Domain authority
       - Backlink + referring domain counts
       - Basic risk/quality indicators.

---

## 🔌 API Contract (Planned Shape)

**Request**

```http
POST /api/analyze
Content-Type: application/json
{
  "url": "https://example.com"
}
Response
{
  "url": "https://example.com",
  "checkedAtUtc": "2025-12-14T17:00:00Z",
  "network": {
    "statusCode": 200,
    "responseTimeMs": 153,
    "ttfbMs": 80,
    "contentLengthBytes": 24567,
    "errorMessage": null
  },
  "performance": {
    "overallScore": 92,
    "mobileScore": 90,
    "desktopScore": 96,
    "largestContentfulPaintMs": 1800,
    "firstContentfulPaintMs": 900,
    "cumulativeLayoutShift": 0.03,
    "totalBlockingTimeMs": 50
  },
  "seo": {
    "title": "Example Site - Home",
    "titleLength": 21,
    "metaDescription": "Example description text...",
    "metaDescriptionLength": 80,
    "canonicalUrl": "https://example.com/",
    "isIndexable": true,
    "h1Count": 1,
    "h2Count": 5
  },
  "offPageSeo": {
    "domainAuthority": 47,
    "backlinks": 1230,
    "referringDomains": 80,
    "spamScore": 5
  }
}
(Fields from external APIs are normalized into this internal format.)

GET /api/history?url=https://example.com
- Returns an array of past scans (most recent first) for the normalized URL.

POST /api/report
- Body matches `/api/analyze`
- Returns structured sections (`summary`, `performance`, `seo`, optional `offPageSeo`, `recommendations`) so the frontend can download/share JSON reports.

GET /api/history/latest?url=https://example.com
- Convenience endpoint that returns only the newest `ScanRecord` (204 No Content if none exist).

⚙️ Configuration
Environment variables (example):
# Backend
ASPNETCORE_ENVIRONMENT=Development
PERFORMANCE_API_KEY=your_pagespeed_or_perf_tool_key
PERFORMANCE_API_BASE_URL=https://www.googleapis.com/pagespeedonline/v5/runPagespeed
SEO_API_KEY=your_seo_tool_key
SEO_API_BASE_URL=https://your-seo-provider.example/api/authority
ALLOWED_FRONTEND_ORIGIN=http://localhost:5173

# Frontend (Vite)
VITE_API_BASE_URL=http://localhost:5218
External services can be toggled on/off based on whether keys are present.
- Scan history is persisted locally under `SiteMonitor/SiteMonitor.Api/App_Data/scan-history.json` (created automatically).
💼 Impact & Use Cases
This app is useful for:
Developers & freelancers
Quickly checking client sites for performance and SEO issues.
Providing visual reports as part of a website audit or redesign proposal.
Showing before/after improvements when optimizing a site.
Small business owners
Getting a simple, understandable snapshot of:
“Is my site fast?”
“Is my page set up correctly for search engines?”
“Is my domain in decent standing?”
As a portfolio project
Demonstrates:
Full-stack skills (.NET + React + TypeScript)
API design and integration with external services
Performance engineering awareness (not just CRUD apps)
SEO and web fundamentals (titles, meta tags, indexability)
Shows you can:
Design an architecture
Orchestrate multiple services
Turn raw data into a clear, visual dashboard
This is more than a basic “todo app” or simple CRUD project—it behaves like a mini version of tools used by real agencies and SEO/DevOps teams.
🚧 Future Enhancements (Planned)
User accounts + saved scan history
Trend charts (performance/SEO over time)
PDF report export for clients
Scheduled automated scans (daily/weekly) with alerts
Comparison mode:
Compare two URLs or two scans of the same URL side-by-side
🏁 Summary
This Website Speed & SEO Performance Analyzer:
Uses React + TypeScript on the frontend for a clean, interactive dashboard.
Uses ASP.NET Core on the backend to coordinate:
Raw HTTP/network checks
External performance tools
On-page HTML/SEO analysis
Optional off-page SEO integrations
Returns a unified analysis object that’s easy to extend and visualize.
Delivers real-world value while showcasing full-stack engineering skills.
If you’re reading this as a reviewer:
This project is intentionally built to demonstrate end-to-end system design,
API integration, performance awareness, and practical web/SEO understanding —
not just basic component or CRUD work.
