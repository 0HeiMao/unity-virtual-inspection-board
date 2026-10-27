import { useCallback, useEffect, useMemo, useState } from 'react'
import { ApiError, fetchIssues, updateIssueStatus } from './api'
import type { Issue, IssueStatus, Priority } from './types'

const statusOptions: Array<{ value: IssueStatus; label: string }> = [
  { value: 'open', label: '待处理' },
  { value: 'in_progress', label: '处理中' },
  { value: 'resolved', label: '已解决' },
]

const priorityLabels: Record<Priority, string> = {
  low: '低',
  medium: '中',
  high: '高',
}

const statusLabels = Object.fromEntries(
  statusOptions.map((option) => [option.value, option.label]),
) as Record<IssueStatus, string>

function formatDate(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return value
  }
  return new Intl.DateTimeFormat('zh-CN', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  }).format(date)
}

function errorMessage(error: unknown) {
  return error instanceof ApiError ? error.message : '发生未知错误，请稍后重试'
}

function App() {
  const [issues, setIssues] = useState<Issue[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [updatingId, setUpdatingId] = useState('')
  const [statusFilter, setStatusFilter] = useState<'all' | IssueStatus>('all')
  const [priorityFilter, setPriorityFilter] = useState<'all' | Priority>('all')
  const [search, setSearch] = useState('')
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null)

  const loadIssues = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      setIssues(await fetchIssues())
      setLastUpdated(new Date())
    } catch (loadError) {
      setError(errorMessage(loadError))
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void loadIssues()
  }, [loadIssues])

  const onUpdateStatus = async (issue: Issue, status: IssueStatus) => {
    setUpdatingId(issue.id)
    setError('')
    setNotice('')
    try {
      const updated = await updateIssueStatus(issue.id, status)
      setIssues((current) =>
        current.map((item) => (item.id === updated.id ? updated : item)),
      )
      setNotice(`“${updated.title}”已更新为${statusLabels[updated.status]}`)
    } catch (updateError) {
      setError(`状态更新失败：${errorMessage(updateError)}`)
    } finally {
      setUpdatingId('')
    }
  }

  const counts = useMemo(
    () => ({
      total: issues.length,
      open: issues.filter((issue) => issue.status === 'open').length,
      inProgress: issues.filter((issue) => issue.status === 'in_progress').length,
      resolved: issues.filter((issue) => issue.status === 'resolved').length,
    }),
    [issues],
  )

  const filteredIssues = useMemo(() => {
    const keyword = search.trim().toLocaleLowerCase('zh-CN')
    return issues.filter((issue) => {
      const matchesStatus =
        statusFilter === 'all' || issue.status === statusFilter
      const matchesPriority =
        priorityFilter === 'all' || issue.priority === priorityFilter
      const matchesSearch =
        !keyword ||
        issue.title.toLocaleLowerCase('zh-CN').includes(keyword) ||
        issue.description.toLocaleLowerCase('zh-CN').includes(keyword)
      return matchesStatus && matchesPriority && matchesSearch
    })
  }, [issues, priorityFilter, search, statusFilter])

  return (
    <div className="app-shell">
      <div className="ambient-grid" aria-hidden="true" />
      <header className="topbar">
        <div className="brand">
          <div className="brand-mark" aria-hidden="true">
            <span />
            <span />
            <span />
          </div>
          <div>
            <p className="eyebrow">VIRTUAL PATROL / OPERATIONS</p>
            <h1>巡检问题管理</h1>
          </div>
        </div>
        <div className="topbar-actions">
          <div className={`connection ${error ? 'is-offline' : ''}`}>
            <span className="connection-dot" />
            {error ? '后端连接异常' : loading ? '正在连接后端' : '数据已同步'}
          </div>
          <button className="button button-secondary" onClick={() => void loadIssues()} disabled={loading}>
            <RefreshIcon />
            {loading ? '刷新中' : '刷新列表'}
          </button>
        </div>
      </header>

      <main>
        <section className="hero">
          <div>
            <p className="section-kicker">现场态势</p>
            <h2>把每一个异常，变成可追踪的行动。</h2>
            <p className="hero-copy">
              Unity 巡检端上报的问题会实时汇总到这里。更新处理状态后，数据由 Go
              服务持久化保存。
            </p>
          </div>
          <div className="sync-meta">
            <span>最近同步</span>
            <strong>
              {lastUpdated
                ? lastUpdated.toLocaleTimeString('zh-CN', {
                    hour: '2-digit',
                    minute: '2-digit',
                    second: '2-digit',
                  })
                : '--:--:--'}
            </strong>
            <small>数据源：localhost:8080</small>
          </div>
        </section>

        <section className="stats" aria-label="问题统计">
          <article className="stat-card stat-total">
            <span>全部问题</span>
            <strong>{counts.total.toString().padStart(2, '0')}</strong>
            <small>现场累计上报</small>
          </article>
          <article className="stat-card stat-open">
            <span>待处理</span>
            <strong>{counts.open.toString().padStart(2, '0')}</strong>
            <small>等待责任人确认</small>
          </article>
          <article className="stat-card stat-progress">
            <span>处理中</span>
            <strong>{counts.inProgress.toString().padStart(2, '0')}</strong>
            <small>已进入处置流程</small>
          </article>
          <article className="stat-card stat-resolved">
            <span>已解决</span>
            <strong>{counts.resolved.toString().padStart(2, '0')}</strong>
            <small>问题闭环完成</small>
          </article>
        </section>

        {error && (
          <div className="alert alert-error" role="alert">
            <div>
              <strong>无法获取最新数据</strong>
              <span>{error}</span>
            </div>
            <button className="button button-danger" onClick={() => void loadIssues()}>
              重试连接
            </button>
          </div>
        )}
        {notice && (
          <div className="alert alert-success" role="status">
            <CheckIcon />
            <span>{notice}</span>
            <button aria-label="关闭提示" onClick={() => setNotice('')}>
              ×
            </button>
          </div>
        )}

        <section className="board">
          <div className="board-heading">
            <div>
              <p className="section-kicker">ISSUE QUEUE</p>
              <h2>问题队列</h2>
            </div>
            <span className="result-count">{filteredIssues.length} 条记录</span>
          </div>

          <div className="filters">
            <label className="search-box">
              <SearchIcon />
              <span className="sr-only">搜索问题</span>
              <input
                type="search"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="搜索标题或描述"
              />
            </label>
            <label>
              <span>状态</span>
              <select
                value={statusFilter}
                onChange={(event) =>
                  setStatusFilter(event.target.value as 'all' | IssueStatus)
                }
              >
                <option value="all">全部状态</option>
                {statusOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </label>
            <label>
              <span>优先级</span>
              <select
                value={priorityFilter}
                onChange={(event) =>
                  setPriorityFilter(event.target.value as 'all' | Priority)
                }
              >
                <option value="all">全部优先级</option>
                <option value="high">高</option>
                <option value="medium">中</option>
                <option value="low">低</option>
              </select>
            </label>
          </div>

          {loading && issues.length === 0 ? (
            <div className="issue-list" aria-label="正在加载">
              {[0, 1, 2].map((item) => (
                <div className="issue-card skeleton" key={item}>
                  <span />
                  <span />
                  <span />
                </div>
              ))}
            </div>
          ) : filteredIssues.length > 0 ? (
            <div className="issue-list">
              {filteredIssues.map((issue) => (
                <article className={`issue-card priority-${issue.priority}`} key={issue.id}>
                  <div className="priority-rail" aria-hidden="true" />
                  <div className="issue-main">
                    <div className="issue-title-row">
                      <div>
                        <div className="issue-tags">
                          <span className={`priority priority-${issue.priority}`}>
                            {priorityLabels[issue.priority]}优先级
                          </span>
                          <span className={`status status-${issue.status}`}>
                            {statusLabels[issue.status]}
                          </span>
                        </div>
                        <h3>{issue.title}</h3>
                      </div>
                      <span className="issue-time">{formatDate(issue.createdAt)}</span>
                    </div>
                    <p className={issue.description ? '' : 'muted'}>
                      {issue.description || '未填写问题描述'}
                    </p>
                    <div className="issue-meta">
                      <code className="coordinate">
                        X {issue.position.x.toFixed(2)}
                        <i />
                        Y {issue.position.y.toFixed(2)}
                        <i />
                        Z {issue.position.z.toFixed(2)}
                      </code>
                      <span className="issue-id">#{issue.id.slice(0, 8)}</span>
                    </div>
                  </div>
                  <label className="status-control">
                    <span>处理状态</span>
                    <div className="select-wrap">
                      <select
                        value={issue.status}
                        disabled={updatingId === issue.id}
                        onChange={(event) =>
                          void onUpdateStatus(
                            issue,
                            event.target.value as IssueStatus,
                          )
                        }
                        aria-label={`修改“${issue.title}”的处理状态`}
                      >
                        {statusOptions.map((option) => (
                          <option key={option.value} value={option.value}>
                            {option.label}
                          </option>
                        ))}
                      </select>
                      <ChevronIcon />
                    </div>
                    {updatingId === issue.id && <small>正在保存...</small>}
                  </label>
                </article>
              ))}
            </div>
          ) : (
            <div className="empty-state">
              <div className="empty-icon" aria-hidden="true">
                <ScanIcon />
              </div>
              <h3>{issues.length === 0 ? '还没有巡检问题' : '没有符合筛选条件的记录'}</h3>
              <p>
                {issues.length === 0
                  ? '在 Unity 虚拟现场中点击设备或地面，即可创建第一条问题记录。'
                  : '调整上方筛选条件后重试。'}
              </p>
            </div>
          )}
        </section>
      </main>

      <footer>
        <span>VIRTUAL INSPECTION CONSOLE</span>
        <span>Unity · Go · React</span>
      </footer>
    </div>
  )
}

function RefreshIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M20 11a8.1 8.1 0 0 0-15.5-2M4 4v5h5M4 13a8.1 8.1 0 0 0 15.5 2M20 20v-5h-5" />
    </svg>
  )
}

function SearchIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <circle cx="11" cy="11" r="7" />
      <path d="m20 20-4-4" />
    </svg>
  )
}

function ChevronIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="m7 10 5 5 5-5" />
    </svg>
  )
}

function CheckIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="m5 12 4 4L19 6" />
    </svg>
  )
}

function ScanIcon() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M4 8V4h4M16 4h4v4M20 16v4h-4M8 20H4v-4M8 9h8v6H8z" />
    </svg>
  )
}

export default App
