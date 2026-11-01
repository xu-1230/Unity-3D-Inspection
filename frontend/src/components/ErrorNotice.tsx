import { IconAlert, IconRefresh } from './Icons';

interface ErrorNoticeProps {
  message: string;
  endpoint: string;
  onRetry: () => void;
}

/** 后端不可用时的兜底视图：保留页面骨架，给出原因、排查线索与重试入口。 */
export default function ErrorNotice({ message, endpoint, onRetry }: ErrorNoticeProps) {
  return (
    <div className="panel" role="alert">
      <div className="state">
        <span className="state__art state__art--error" aria-hidden="true">
          <IconAlert />
        </span>
        <p className="state__title">加载失败，无法取得问题列表</p>
        <p className="state__desc">
          {message}
          <br />
          页面框架保持可用。确认 Go 服务已启动后点击重试，或检查下方接口是否可访问。
        </p>
        <div className="state__code">GET {endpoint}</div>
        <div className="state__actions">
          <button type="button" className="btn btn--primary btn--sm" onClick={onRetry}>
            <IconRefresh size={15} />
            重试
          </button>
        </div>
      </div>
    </div>
  );
}
