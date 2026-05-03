import { useState } from 'react'
import './App.css'

const API_URL = 'http://localhost:5223/api/Build/generate'

const WORKLOADS = ['Gaming', 'Video Editing', 'AI']

const WORKLOAD_ICONS = {
  'Gaming':        '🎮',
  'Video Editing': '🎬',
  'AI':            '🤖'
}

const PART_CONFIG = {
  'CPU':         { icon: '⚙️',  color: '#00e5ff' },
  'GPU':         { icon: '🖥️',  color: '#a855f7' },
  'RAM':         { icon: '💾',  color: '#00ff88' },
  'Motherboard': { icon: '🔌',  color: '#ff6b35' },
  'Storage':     { icon: '💿',  color: '#fbbf24' },
  'PSU':         { icon: '⚡',  color: '#f472b6' },
}

function parseReview(text) {
  const overview     = text.match(/OVERVIEW:\s*(.+?)(?=\nCPU:)/s)?.[1]?.trim()
  const cpu          = text.match(/CPU:\s*(.+?)(?=\nGPU:)/s)?.[1]?.trim()
  const gpu          = text.match(/GPU:\s*(.+?)(?=\nRAM:)/s)?.[1]?.trim()
  const ram          = text.match(/RAM:\s*(.+?)(?=\nMOTHERBOARD:)/s)?.[1]?.trim()
  const motherboard  = text.match(/MOTHERBOARD:\s*(.+?)(?=\nSTORAGE:)/s)?.[1]?.trim()
  const storage      = text.match(/STORAGE:\s*(.+?)(?=\nPSU:)/s)?.[1]?.trim()
  const psu          = text.match(/PSU:\s*(.+?)(?=\nBEST FOR:)/s)?.[1]?.trim()
  const bestFor      = text.match(/BEST FOR:\s*(.+?)$/s)?.[1]?.trim()
  return { overview, cpu, gpu, ram, motherboard, storage, psu, bestFor }
}

export default function App() {
  const [budget, setBudget]     = useState('')
  const [workload, setWorkload] = useState('Gaming')
  const [build, setBuild]       = useState(null)
  const [loading, setLoading]   = useState(false)
  const [error, setError]       = useState('')

  async function handleGenerate() {
    if (!budget || isNaN(budget) || Number(budget) <= 0) {
      setError('Please enter a valid budget.')
      return
    }
    setLoading(true)
    setError('')
    setBuild(null)

    try {
      const res = await fetch(API_URL, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ budget: Number(budget), workload })
      })

      if (!res.ok) {
        const err = await res.json()
        setError(err.error || 'Something went wrong.')
        return
      }

      const data = await res.json()
      setBuild(data)
    } catch (e) {
      setError('Could not reach the API. Make sure the server is running.')
    } finally {
      setLoading(false)
    }
  }

  const review = build?.aiReview ? parseReview(build.aiReview) : null

  // Map part type to review key
  const partReviewKey = {
    'CPU': 'cpu', 'GPU': 'gpu', 'RAM': 'ram',
    'Motherboard': 'motherboard', 'Storage': 'storage', 'PSU': 'psu'
  }

  return (
    <div className="app">
      <div className="bg-grid" />

      <div className="container">

        <header className="header">
          <div className="logo-block">
            <span className="logo-icon">◈</span>
            <h1 className="logo-text">BUILDFORGE</h1>
          </div>
          <p className="tagline">AI-Optimized PC Builds</p>
        </header>

        <div className="form-card">
          <div className="form-row">
            <div className="field">
              <label>BUDGET (USD)</label>
              <div className="input-wrap">
                <span className="input-prefix">$</span>
                <input
                  type="number"
                  placeholder="1000"
                  value={budget}
                  onChange={e => setBudget(e.target.value)}
                  onKeyDown={e => e.key === 'Enter' && handleGenerate()}
                />
              </div>
            </div>

            <div className="field">
              <label>WORKLOAD</label>
              <div className="workload-tabs">
                {WORKLOADS.map(w => (
                  <button
                    key={w}
                    className={`tab ${workload === w ? 'active' : ''}`}
                    onClick={() => setWorkload(w)}
                  >
                    <span className="tab-icon">{WORKLOAD_ICONS[w]}</span>
                    <span className="tab-label">{w}</span>
                  </button>
                ))}
              </div>
            </div>
          </div>

          <button
            className="generate-btn"
            onClick={handleGenerate}
            disabled={loading}
          >
            {loading ? (
              <span className="btn-loading">
                <span className="spinner" />
                Generating...
              </span>
            ) : 'Generate Build →'}
          </button>
        </div>

        {error && <div className="error-card">⚠ {error}</div>}

        {build && (
          <div className="result-card">

            <div className="result-header">
              <div>
                <div className="result-label">RECOMMENDED BUILD</div>
                <div className="result-workload">
                  {WORKLOAD_ICONS[workload]} {workload}
                </div>
              </div>
              <div className="result-price">${build.totalPrice.toFixed(2)}</div>
            </div>

            {/* Overview */}
            {review?.overview && (
              <div className="overview-block">
                <p className="overview-text">{review.overview}</p>
              </div>
            )}

            <div className="disclaimer">
              ℹ Prices are approximate. Check current prices before purchasing.
            </div>

            {/* Parts with inline AI descriptions */}
            <div className="parts-grid">
              {build.parts.map((part, i) => {
                const config     = PART_CONFIG[part.type] || { icon: '🔧', color: '#888' }
                const reviewKey  = partReviewKey[part.type]
                const partReview = review?.[reviewKey]

                return (
                  <div
                    className="part-card"
                    key={i}
                    style={{ animationDelay: `${i * 60}ms` }}
                  >
                    <div
                      className="part-icon-box"
                      style={{
                        background:  `${config.color}18`,
                        borderColor: `${config.color}40`
                      }}
                    >
                      <span className="part-icon">{config.icon}</span>
                    </div>
                    <div className="part-info">
                      <div className="part-type" style={{ color: config.color }}>
                        {part.type}
                      </div>
                      <div className="part-name">{part.name}</div>
                      <div className="part-price">${part.price.toFixed(2)}</div>
                      {partReview && (
                        <div className="part-review">{partReview}</div>
                      )}
                    </div>
                  </div>
                )
              })}
            </div>

            {/* Best For */}
            {review?.bestFor && (
              <div className="best-for-block">
                <span className="best-for-label">◆ BEST FOR</span>
                <span className="best-for-text">{review.bestFor}</span>
              </div>
            )}

          </div>
        )}

      </div>
    </div>
  )
}