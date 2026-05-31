import { useState, useEffect, createContext, useContext } from 'react'
import { BrowserRouter, Routes, Route, Navigate, useNavigate, Link } from 'react-router-dom'
import './App.css'

const BASE_URL = 'http://localhost:5223'

// ─── Auth Context ─────────────────────────────────────────────────────────────
const AuthContext = createContext(null)

function AuthProvider({ children }) {
  const [user, setUser] = useState(() => {
    const saved = localStorage.getItem('bf_user')
    return saved ? JSON.parse(saved) : null
  })
  function login(userData) { localStorage.setItem('bf_user', JSON.stringify(userData)); setUser(userData) }
  function logout() { localStorage.removeItem('bf_user'); setUser(null) }
  return <AuthContext.Provider value={{ user, login, logout }}>{children}</AuthContext.Provider>
}

function useAuth() { return useContext(AuthContext) }

function Protected({ children }) {
  const { user } = useAuth()
  return user ? children : <Navigate to="/login" replace />
}

// ─── Nav ──────────────────────────────────────────────────────────────────────

function Nav() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  return (
    <nav className="nav">
      <Link to="/" className="nav-logo">◈ BUILDFORGE</Link>
      <div className="nav-links">
        <Link to="/parts" className="nav-link">⚙ Parts</Link>
        {user ? (
          <>
            <Link to="/profile" className="nav-link">⊞ {user.username}</Link>
            <button className="nav-btn" onClick={() => { logout(); navigate('/') }}>Sign Out</button>
          </>
        ) : (
          <>
            <Link to="/login"    className="nav-link">Sign In</Link>
            <Link to="/register" className="nav-btn-outline">Register</Link>
          </>
        )}
      </div>
    </nav>
  )
}
// ─── Constants ────────────────────────────────────────────────────────────────
const WORKLOADS     = ['Gaming', 'Video Editing', 'AI']
const WORKLOAD_ICONS = { 'Gaming': '🎮', 'Video Editing': '🎬', 'AI': '🤖' }
const PART_CONFIG   = {
  'CPU':         { icon: '⚙️',  color: '#00e5ff' },
  'GPU':         { icon: '🖥️',  color: '#a855f7' },
  'RAM':         { icon: '💾',  color: '#00ff88' },
  'Motherboard': { icon: '🔌',  color: '#ff6b35' },
  'Storage':     { icon: '💿',  color: '#fbbf24' },
  'PSU':         { icon: '⚡',  color: '#f472b6' },
}
const PART_REVIEW_KEY = { CPU: 'cpu', GPU: 'gpu', RAM: 'ram', Motherboard: 'motherboard', Storage: 'storage', PSU: 'psu' }

function parseReview(text) {
  return {
    overview:    text.match(/OVERVIEW:\s*(.+?)(?=\nCPU:)/s)?.[1]?.trim(),
    cpu:         text.match(/CPU:\s*(.+?)(?=\nGPU:)/s)?.[1]?.trim(),
    gpu:         text.match(/GPU:\s*(.+?)(?=\nRAM:)/s)?.[1]?.trim(),
    ram:         text.match(/RAM:\s*(.+?)(?=\nMOTHERBOARD:)/s)?.[1]?.trim(),
    motherboard: text.match(/MOTHERBOARD:\s*(.+?)(?=\nSTORAGE:)/s)?.[1]?.trim(),
    storage:     text.match(/STORAGE:\s*(.+?)(?=\nPSU:)/s)?.[1]?.trim(),
    psu:         text.match(/PSU:\s*(.+?)(?=\nBEST FOR:)/s)?.[1]?.trim(),
    bestFor:     text.match(/BEST FOR:\s*(.+?)$/s)?.[1]?.trim(),
  }
}

// ─── Home ─────────────────────────────────────────────────────────────────────
function HomePage() {
  const { user }                     = useAuth()
  const [budget,   setBudget]        = useState('')
  const [workload, setWorkload]      = useState('Gaming')
  const [build,    setBuild]         = useState(null)
  const [loading,  setLoading]       = useState(false)
  const [error,    setError]         = useState('')
  const [saved,    setSaved]         = useState(false)
  const [saving,   setSaving]        = useState(false)

  async function handleGenerate() {
    if (!budget || isNaN(budget) || Number(budget) <= 0) { setError('Please enter a valid budget.'); return }
    setLoading(true); setError(''); setBuild(null); setSaved(false)
    try {
      const res = await fetch(`${BASE_URL}/api/Build/generate`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ budget: Number(budget), workload })
      })
      if (!res.ok) { const e = await res.json(); setError(e.error || 'Something went wrong.'); return }
      setBuild(await res.json())
    } catch { setError('Could not reach the API.') } finally { setLoading(false) }
  }

  async function handleSave() {
    if (!user || !build) return
    setSaving(true)
    try {
      const res = await fetch(`${BASE_URL}/api/SavedBuilds`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${user.token}` },
        body: JSON.stringify({ name: `${workload} $${budget}`, workload, budget: Number(budget), totalPrice: build.totalPrice, parts: build.parts })
      })
      if (res.ok) setSaved(true)
    } catch { setError('Could not save build.') } finally { setSaving(false) }
  }

  const review = build?.aiReview ? parseReview(build.aiReview) : null

  return (
    <div className="page">
      <header className="hero">
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
              <input type="number" placeholder="1000" value={budget}
                onChange={e => setBudget(e.target.value)}
                onKeyDown={e => e.key === 'Enter' && handleGenerate()} />
            </div>
          </div>
          <div className="field">
            <label>WORKLOAD</label>
            <div className="workload-tabs">
              {WORKLOADS.map(w => (
                <button key={w} className={`tab ${workload === w ? 'active' : ''}`} onClick={() => setWorkload(w)}>
                  <span className="tab-icon">{WORKLOAD_ICONS[w]}</span>
                  <span className="tab-label">{w}</span>
                </button>
              ))}
            </div>
          </div>
        </div>
        <button className="generate-btn" onClick={handleGenerate} disabled={loading}>
          {loading ? <span className="btn-loading"><span className="spinner" />Generating...</span> : 'Generate Build →'}
        </button>
      </div>

      {error && <div className="error-card">⚠ {error}</div>}

      {build && (
        <div className="result-card">
          <div className="result-header">
            <div>
              <div className="result-label">RECOMMENDED BUILD</div>
              <div className="result-workload">{WORKLOAD_ICONS[workload]} {workload}</div>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
              <div className="result-price">${build.totalPrice.toFixed(2)}</div>
              {user && (
                <button className={`save-btn ${saved ? 'saved' : ''}`} onClick={handleSave} disabled={saving || saved}>
                  {saved ? '✓ Saved' : saving ? 'Saving...' : '+ Save Build'}
                </button>
              )}
            </div>
          </div>

          {review?.overview && <div className="overview-block"><p className="overview-text">{review.overview}</p></div>}
          <div className="disclaimer">ℹ Prices are approximate. Check current prices before purchasing.</div>

          <div className="parts-grid">
            {build.parts.map((part, i) => {
              const cfg = PART_CONFIG[part.type] || { icon: '🔧', color: '#888' }
              return (
                <div className="part-card" key={i} style={{ animationDelay: `${i * 60}ms` }}>
                  <div className="part-icon-box" style={{ background: `${cfg.color}18`, borderColor: `${cfg.color}40` }}>
                    <span className="part-icon">{cfg.icon}</span>
                  </div>
                  <div className="part-info">
                    <div className="part-type" style={{ color: cfg.color }}>{part.type}</div>
                    <div className="part-name">{part.name}</div>
                    <div className="part-price">${part.price.toFixed(2)}</div>
                    {review?.[PART_REVIEW_KEY[part.type]] && <div className="part-review">{review[PART_REVIEW_KEY[part.type]]}</div>}
                  </div>
                </div>
              )
            })}
          </div>

          {review?.bestFor && (
            <div className="best-for-block">
              <span className="best-for-label">◆ BEST FOR</span>
              <span className="best-for-text">{review.bestFor}</span>
            </div>
          )}

          {!user && <div className="login-prompt"><Link to="/login">Sign in</Link> to save this build to your profile.</div>}
        </div>
      )}
    </div>
  )
}

// ─── Login ────────────────────────────────────────────────────────────────────
function LoginPage() {
  const { login } = useAuth()
  const navigate  = useNavigate()
  const [email,    setEmail]    = useState('')
  const [password, setPassword] = useState('')
  const [error,    setError]    = useState('')
  const [loading,  setLoading]  = useState(false)

  async function handleLogin() {
    if (!email || !password) { setError('Please fill in all fields.'); return }
    setLoading(true); setError('')
    try {
      const res  = await fetch(`${BASE_URL}/api/Auth/login`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password })
      })
      const data = await res.json()
      if (!res.ok) { setError(data.error || 'Login failed.'); return }
      login({ token: data.token, username: data.username, email: data.email, id: data.id })
      navigate('/')
    } catch { setError('Could not reach the server.') } finally { setLoading(false) }
  }

  return (
    <div className="page auth-page">
      <div className="auth-card">
        <div className="auth-logo">◈ BUILDFORGE</div>
        <h2 className="auth-title">Sign In</h2>
        {error && <div className="error-card">⚠ {error}</div>}
        <div className="auth-field"><label>EMAIL</label>
          <input type="email" placeholder="you@example.com" value={email} onChange={e => setEmail(e.target.value)} onKeyDown={e => e.key === 'Enter' && handleLogin()} />
        </div>
        <div className="auth-field"><label>PASSWORD</label>
          <input type="password" placeholder="••••••••" value={password} onChange={e => setPassword(e.target.value)} onKeyDown={e => e.key === 'Enter' && handleLogin()} />
        </div>
        <button className="generate-btn" onClick={handleLogin} disabled={loading}>
          {loading ? <span className="btn-loading"><span className="spinner" />Signing in...</span> : 'Sign In →'}
        </button>
        <p className="auth-switch">Don't have an account? <Link to="/register">Register</Link></p>
      </div>
    </div>
  )
}

// ─── Register ─────────────────────────────────────────────────────────────────
function RegisterPage() {
  const { login } = useAuth()
  const navigate  = useNavigate()
  const [username, setUsername] = useState('')
  const [email,    setEmail]    = useState('')
  const [password, setPassword] = useState('')
  const [error,    setError]    = useState('')
  const [loading,  setLoading]  = useState(false)

  async function handleRegister() {
    if (!username || !email || !password) { setError('Please fill in all fields.'); return }
    if (password.length < 6) { setError('Password must be at least 6 characters.'); return }
    setLoading(true); setError('')
    try {
      const res  = await fetch(`${BASE_URL}/api/Auth/register`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, email, password })
      })
      const data = await res.json()
      if (!res.ok) { setError(data.error || 'Registration failed.'); return }
      login({ token: data.token, username, email, id: data.id })
      navigate('/')
    } catch { setError('Could not reach the server.') } finally { setLoading(false) }
  }

  return (
    <div className="page auth-page">
      <div className="auth-card">
        <div className="auth-logo">◈ BUILDFORGE</div>
        <h2 className="auth-title">Create Account</h2>
        {error && <div className="error-card">⚠ {error}</div>}
        <div className="auth-field"><label>USERNAME</label>
          <input type="text" placeholder="YourName" value={username} onChange={e => setUsername(e.target.value)} />
        </div>
        <div className="auth-field"><label>EMAIL</label>
          <input type="email" placeholder="you@example.com" value={email} onChange={e => setEmail(e.target.value)} />
        </div>
        <div className="auth-field"><label>PASSWORD</label>
          <input type="password" placeholder="Min 6 characters" value={password} onChange={e => setPassword(e.target.value)} onKeyDown={e => e.key === 'Enter' && handleRegister()} />
        </div>
        <button className="generate-btn" onClick={handleRegister} disabled={loading}>
          {loading ? <span className="btn-loading"><span className="spinner" />Creating account...</span> : 'Create Account →'}
        </button>
        <p className="auth-switch">Already have an account? <Link to="/login">Sign In</Link></p>
      </div>
    </div>
  )
}

// ─── Profile ──────────────────────────────────────────────────────────────────
function ProfilePage() {
  const { user }             = useAuth()
  const navigate             = useNavigate()
  const [builds,   setBuilds]   = useState([])
  const [loading,  setLoading]  = useState(true)
  const [selected, setSelected] = useState([])

  useEffect(() => {
    async function fetchBuilds() {
      try {
        const res = await fetch(`${BASE_URL}/api/SavedBuilds`, { headers: { 'Authorization': `Bearer ${user.token}` } })
        if (res.ok) setBuilds(await res.json())
      } finally { setLoading(false) }
    }
    fetchBuilds()
  }, [])

  async function handleDelete(id) {
    const res = await fetch(`${BASE_URL}/api/SavedBuilds/${id}`, { method: 'DELETE', headers: { 'Authorization': `Bearer ${user.token}` } })
    if (res.ok) setBuilds(prev => prev.filter(b => b.id !== id))
  }

  function toggleSelect(id) {
    setSelected(prev => prev.includes(id) ? prev.filter(x => x !== id) : prev.length < 2 ? [...prev, id] : prev)
  }

  return (
    <div className="page">
      <div className="profile-header">
        <div>
          <h2 className="profile-title">⊞ {user.username}'s Builds</h2>
          <p className="profile-sub">{user.email}</p>
        </div>
      </div>

      {selected.length > 0 && (
        <div className="compare-bar">
          <span>{selected.length} build{selected.length > 1 ? 's' : ''} selected</span>
          <button className="generate-btn" style={{ padding: '10px 24px', fontSize: '14px' }}
            onClick={() => navigate(`/compare?id1=${selected[0]}&id2=${selected[1]}`)} disabled={selected.length < 2}>
            Compare Selected →
          </button>
          <button className="nav-btn" onClick={() => setSelected([])}>Clear</button>
        </div>
      )}

      {loading ? (
        <div className="loading-text">Loading builds...</div>
      ) : builds.length === 0 ? (
        <div className="empty-state">
          <p>No saved builds yet.</p>
          <Link to="/" className="generate-btn" style={{ display: 'inline-block', marginTop: '16px', textDecoration: 'none', padding: '14px 32px' }}>Generate a Build →</Link>
        </div>
      ) : (
        <div className="builds-grid">
          {builds.map(b => (
            <div key={b.id} className={`build-card ${selected.includes(b.id) ? 'selected' : ''}`} onClick={() => toggleSelect(b.id)}>
              <div className="build-card-header">
                <span className="build-workload">{WORKLOAD_ICONS[b.workload]} {b.workload}</span>
                <span className="build-price">${b.totalPrice.toFixed(2)}</span>
              </div>
              <div className="build-name">{b.name}</div>
              <div className="build-date">{new Date(b.createdAt).toLocaleDateString()}</div>
              <div className="build-parts-preview">
                {b.parts?.slice(0, 3).map((p, i) => (
                  <div key={i} className="build-part-row">
                    <span style={{ color: PART_CONFIG[p.type]?.color || '#888' }}>{p.type}</span>
                    <span>{p.name}</span>
                  </div>
                ))}
              </div>
              {selected.includes(b.id) && <div className="selected-badge">✓ Selected for Compare</div>}
              <button className="delete-btn" onClick={e => { e.stopPropagation(); handleDelete(b.id) }}>✕ Delete</button>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

// ─── Compare ──────────────────────────────────────────────────────────────────
function ComparePage() {
  const { user }              = useAuth()
  const navigate              = useNavigate()
  const params                = new URLSearchParams(window.location.search)
  const id1                   = params.get('id1')
  const id2                   = params.get('id2')
  const [data,    setData]    = useState(null)
  const [loading, setLoading] = useState(true)
  const [error,   setError]   = useState('')

  useEffect(() => {
    async function load() {
      try {
        const res = await fetch(`${BASE_URL}/api/SavedBuilds/compare?id1=${id1}&id2=${id2}`, { headers: { 'Authorization': `Bearer ${user.token}` } })
        if (!res.ok) { setError('Could not load builds.'); return }
        setData(await res.json())
      } catch { setError('Could not reach the server.') } finally { setLoading(false) }
    }
    load()
  }, [])

  if (loading) return <div className="page"><div className="loading-text">Loading comparison...</div></div>
  if (error)   return <div className="page"><div className="error-card">⚠ {error}</div></div>

  const PART_TYPES = ['CPU', 'GPU', 'RAM', 'Motherboard', 'Storage', 'PSU']

  return (
    <div className="page">
      <div className="compare-header">
        <button className="nav-link" onClick={() => navigate('/profile')}>← Back to Profile</button>
        <h2 className="profile-title">Build Comparison</h2>
      </div>

      <div className="compare-table">
        <div className="compare-row compare-head">
          <div className="compare-cell label-cell">COMPONENT</div>
          <div className="compare-cell">{data.build1.name}</div>
          <div className="compare-cell">{data.build2.name}</div>
        </div>

        <div className="compare-row highlight-row">
          <div className="compare-cell label-cell">TOTAL PRICE</div>
          <div className={`compare-cell ${data.build1.totalPrice <= data.build2.totalPrice ? 'winner' : ''}`}>
            ${data.build1.totalPrice.toFixed(2)}
            {data.build1.totalPrice < data.build2.totalPrice && <span className="win-badge">✓ Cheaper</span>}
          </div>
          <div className={`compare-cell ${data.build2.totalPrice <= data.build1.totalPrice ? 'winner' : ''}`}>
            ${data.build2.totalPrice.toFixed(2)}
            {data.build2.totalPrice < data.build1.totalPrice && <span className="win-badge">✓ Cheaper</span>}
          </div>
        </div>

        <div className="compare-row">
          <div className="compare-cell label-cell">WORKLOAD</div>
          <div className="compare-cell">{WORKLOAD_ICONS[data.build1.workload]} {data.build1.workload}</div>
          <div className="compare-cell">{WORKLOAD_ICONS[data.build2.workload]} {data.build2.workload}</div>
        </div>

        {PART_TYPES.map(type => {
          const p1    = data.build1.parts?.find(p => p.type === type)
          const p2    = data.build2.parts?.find(p => p.type === type)
          const color = PART_CONFIG[type]?.color || '#888'
          return (
            <div className="compare-row" key={type}>
              <div className="compare-cell label-cell" style={{ color }}>{PART_CONFIG[type]?.icon} {type}</div>
              <div className="compare-cell">
                <div className="compare-part-name">{p1?.name || '—'}</div>
                {p1 && <div className="compare-part-price">${p1.price.toFixed(2)}</div>}
              </div>
              <div className="compare-cell">
                <div className="compare-part-name">{p2?.name || '—'}</div>
                {p2 && <div className="compare-part-price">${p2.price.toFixed(2)}</div>}
              </div>
            </div>
          )
        })}
      </div>
    </div>
  )
}

// ─── Parts Page ───────────────────────────────────────────────────────────────
const PART_TYPES = ['CPU', 'GPU', 'Motherboard', 'RAM', 'Storage', 'PSU']


const EMPTY_PART = {
  type: 'GPU', name: '', price: '', performanceScore: '',
  socket: '', ramType: '', sizeGb: '', capacityGb: '', wattage: '', imageUrl: ''
}

function PartsPage() {
  const [parts,     setParts]     = useState([])
  const [loading,   setLoading]   = useState(true)
  const [filter,    setFilter]    = useState('All')
  const [showForm,  setShowForm]  = useState(false)
  const [editId,    setEditId]    = useState(null)
  const [form,      setForm]      = useState(EMPTY_PART)
  const [error,     setError]     = useState('')
  const [success,   setSuccess]   = useState('')

  useEffect(() => { fetchParts() }, [])

  async function fetchParts() {
    setLoading(true)
    try {
      const res = await fetch(`${BASE_URL}/api/Parts`)
      if (res.ok) setParts(await res.json())
    } finally { setLoading(false) }
  }

  async function handleSave() {
    setError(''); setSuccess('')
    if (!form.name || !form.type || !form.price) { setError('Name, Type and Price are required.'); return }

    const body = {
      ...form,
      price:            Number(form.price),
      performanceScore: Number(form.performanceScore) || 0,
      sizeGb:           form.sizeGb      ? Number(form.sizeGb)      : null,
      capacityGb:       form.capacityGb  ? Number(form.capacityGb)  : null,
      wattage:          form.wattage     ? Number(form.wattage)      : null,
    }

    const url    = editId ? `${BASE_URL}/api/Parts/${editId}` : `${BASE_URL}/api/Parts`
    const method = editId ? 'PUT' : 'POST'

    try {
      const res = await fetch(url, {
        method, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body)
      })
      if (!res.ok) { const e = await res.json(); setError(e.error || 'Failed.'); return }
      setSuccess(editId ? 'Part updated!' : 'Part added!')
      setShowForm(false); setEditId(null); setForm(EMPTY_PART)
      fetchParts()
    } catch { setError('Could not reach the server.') }
  }

  async function handleDelete(id) {
    if (!window.confirm('Delete this part?')) return
    try {
      const res = await fetch(`${BASE_URL}/api/Parts/${id}`, { method: 'DELETE' })
      if (res.ok) { setParts(prev => prev.filter(p => p.id !== id)); setSuccess('Part deleted.') }
    } catch { setError('Could not delete part.') }
  }

  function handleEdit(part) {
    setForm({
      type:             part.type,
      name:             part.name,
      price:            part.price,
      performanceScore: part.performanceScore,
      socket:           part.socket    || '',
      ramType:          part.ramType   || '',
      sizeGb:           part.sizeGb    || '',
      capacityGb:       part.capacityGb || '',
      wattage:          part.wattage   || '',
      imageUrl:         part.imageUrl  || ''
    })
    setEditId(part.id)
    setShowForm(true)
    setError(''); setSuccess('')
  }

  const filtered = filter === 'All' ? parts : parts.filter(p => p.type === filter)

  return (
    <div className="page">
      <div className="profile-header">
        <div>
          <h2 className="profile-title">⚙ Parts Manager</h2>
          <p className="profile-sub">{parts.length} parts in database</p>
        </div>
        <button className="generate-btn" style={{ width: 'auto', padding: '12px 24px' }}
          onClick={() => { setShowForm(!showForm); setEditId(null); setForm(EMPTY_PART); setError(''); setSuccess('') }}>
          {showForm ? 'Cancel' : '+ Add Part'}
        </button>
      </div>

      {error   && <div className="error-card">⚠ {error}</div>}
      {success && <div className="success-card">✓ {success}</div>}

      {/* Add / Edit Form */}
      {showForm && (
        <div className="parts-form-card">
          <h3 className="form-title">{editId ? 'Edit Part' : 'Add New Part'}</h3>
          <div className="parts-form-grid">
            <div className="pf-field">
              <label>TYPE *</label>
              <select value={form.type} onChange={e => setForm(f => ({ ...f, type: e.target.value }))}>
                {PART_TYPES.map(t => <option key={t} value={t}>{t}</option>)}
              </select>
            </div>
            <div className="pf-field pf-wide">
              <label>NAME *</label>
              <input type="text" placeholder="e.g. NVIDIA RTX 5090" value={form.name}
                onChange={e => setForm(f => ({ ...f, name: e.target.value }))} />
            </div>
            <div className="pf-field">
              <label>PRICE ($) *</label>
              <input type="number" placeholder="299" value={form.price}
                onChange={e => setForm(f => ({ ...f, price: e.target.value }))} />
            </div>
            <div className="pf-field">
              <label>PERFORMANCE SCORE</label>
              <input type="number" placeholder="0-100" value={form.performanceScore}
                onChange={e => setForm(f => ({ ...f, performanceScore: e.target.value }))} />
            </div>
            <div className="pf-field">
              <label>SOCKET (CPU/MB)</label>
              <input type="text" placeholder="AM5, LGA1700" value={form.socket}
                onChange={e => setForm(f => ({ ...f, socket: e.target.value }))} />
            </div>
            <div className="pf-field">
              <label>RAM TYPE (RAM/MB)</label>
              <input type="text" placeholder="DDR4, DDR5" value={form.ramType}
                onChange={e => setForm(f => ({ ...f, ramType: e.target.value }))} />
            </div>
            <div className="pf-field">
              <label>VRAM GB (GPU)</label>
              <input type="number" placeholder="8" value={form.sizeGb}
                onChange={e => setForm(f => ({ ...f, sizeGb: e.target.value }))} />
            </div>
            <div className="pf-field">
              <label>CAPACITY GB (Storage/RAM)</label>
              <input type="number" placeholder="1000" value={form.capacityGb}
                onChange={e => setForm(f => ({ ...f, capacityGb: e.target.value }))} />
            </div>
            <div className="pf-field">
              <label>WATTAGE (GPU/PSU)</label>
              <input type="number" placeholder="320" value={form.wattage}
                onChange={e => setForm(f => ({ ...f, wattage: e.target.value }))} />
            </div>
            <div className="pf-field pf-wide">
              <label>IMAGE URL</label>
              <input type="text" placeholder="https://..." value={form.imageUrl}
                onChange={e => setForm(f => ({ ...f, imageUrl: e.target.value }))} />
            </div>
          </div>
          <button className="generate-btn" style={{ marginTop: '20px' }} onClick={handleSave}>
            {editId ? 'Save Changes →' : 'Add Part →'}
          </button>
        </div>
      )}

      {/* Filter tabs */}
      <div className="type-filter">
        {['All', ...PART_TYPES].map(t => (
          <button key={t} className={`type-tab ${filter === t ? 'active' : ''}`} onClick={() => setFilter(t)}>
            {t}
          </button>
        ))}
      </div>

      {/* Parts table */}
      {loading ? (
        <div className="loading-text">Loading parts...</div>
      ) : (
        <div className="parts-table">
          <div className="pt-header">
            <div>TYPE</div>
            <div>NAME</div>
            <div>PRICE</div>
            <div>SCORE</div>
            <div>DETAILS</div>
            <div>ACTIONS</div>
          </div>
          {filtered.map(p => (
            <div className="pt-row" key={p.id}>
              <div className="pt-type" style={{ color: PART_CONFIG[p.type]?.color || '#888' }}>
                {PART_CONFIG[p.type]?.icon} {p.type}
              </div>
              <div className="pt-name">{p.name}</div>
              <div className="pt-price">${p.price.toFixed(2)}</div>
              <div className="pt-score">{p.performanceScore}</div>
              <div className="pt-details">
                {p.socket     && <span>Socket: {p.socket}</span>}
                {p.ramType    && <span>RAM: {p.ramType}</span>}
                {p.sizeGb     && <span>VRAM: {p.sizeGb}GB</span>}
                {p.capacityGb && <span>Cap: {p.capacityGb}GB</span>}
                {p.wattage    && <span>{p.wattage}W</span>}
              </div>
              <div className="pt-actions">
                <button className="pt-edit-btn"   onClick={() => handleEdit(p)}>Edit</button>
                <button className="pt-delete-btn" onClick={() => handleDelete(p.id)}>✕</button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
// ─── Root ─────────────────────────────────────────────────────────────────────
export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <div className="app">
          <div className="bg-grid" />
          <Nav />
          <Routes>
            <Route path="/"         element={<HomePage />} />
            <Route path="/login"    element={<LoginPage />} />
            <Route path="/register" element={<RegisterPage />} />
            <Route path="/profile"  element={<Protected><ProfilePage /></Protected>} />
            <Route path="/compare"  element={<Protected><ComparePage /></Protected>} />
	    <Route path="/parts" element={<PartsPage />} />
          </Routes>
        </div>
      </BrowserRouter>
    </AuthProvider>
  )
}