import { Component, type ErrorInfo, type ReactNode } from 'react';
import { IconAlert } from './Icons';

interface ErrorBoundaryProps {
  children: ReactNode;
}

interface ErrorBoundaryState {
  error: Error | null;
}

/** 渲染期异常的兜底，确保任何组件抛错时页面仍保留可读提示而不是白屏。 */
export default class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = { error: null };

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('[管理端] 渲染异常:', error, info.componentStack);
  }

  render() {
    const { error } = this.state;
    if (!error) {
      return this.props.children;
    }

    return (
      <div
        style={{
          display: 'grid',
          placeItems: 'center',
          minHeight: '100vh',
          padding: 24,
        }}
      >
        <div className="panel" style={{ maxWidth: 560, width: '100%' }}>
          <div className="state" role="alert">
            <span className="state__art state__art--error" aria-hidden="true">
              <IconAlert />
            </span>
            <p className="state__title">页面渲染出错</p>
            <p className="state__desc">
              界面在渲染时遇到异常，数据本身可能完好。重新加载后通常可以恢复；若持续出现，请查看浏览器控制台。
            </p>
            <div className="state__code">{error.message}</div>
            <div className="state__actions">
              <button type="button" className="btn btn--primary btn--sm" onClick={() => window.location.reload()}>
                重新加载页面
              </button>
            </div>
          </div>
        </div>
      </div>
    );
  }
}
