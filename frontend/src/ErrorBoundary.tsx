import { Component, type ErrorInfo, type ReactNode } from 'react'

interface Props {
  children: ReactNode
}

interface State {
  hasError: boolean
}

class ErrorBoundary extends Component<Props, State> {
  state: State = { hasError: false }

  static getDerivedStateFromError(): State {
    return { hasError: true }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Admin page render error', error, info)
  }

  render() {
    if (this.state.hasError) {
      return (
        <main className="fatal-error">
          <p className="eyebrow">ADMIN CONSOLE</p>
          <h1>页面加载失败</h1>
          <p>管理端遇到未处理的异常，数据未受影响。请刷新页面重试。</p>
          <button className="button button-primary" onClick={() => window.location.reload()}>
            刷新页面
          </button>
        </main>
      )
    }
    return this.props.children
  }
}

export default ErrorBoundary
