import { useEffect, useRef } from 'react';
import { IconMenu, IconRefresh, IconSearch } from './Icons';

interface TopbarProps {
  title: string;
  subtitle: string;
  query: string;
  refreshing: boolean;
  onQueryChange: (value: string) => void;
  onRefresh: () => void;
  onOpenMobileNav: () => void;
}

/** 顶栏：页面标题、全局检索（Ctrl/Cmd+K）与刷新入口。 */
export default function Topbar({
  title,
  subtitle,
  query,
  refreshing,
  onQueryChange,
  onRefresh,
  onOpenMobileNav,
}: TopbarProps) {
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault();
        inputRef.current?.focus();
        inputRef.current?.select();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, []);

  return (
    <header className="topbar">
      <button
        type="button"
        className="icon-btn sidebar-toggle"
        aria-label="打开导航"
        onClick={onOpenMobileNav}
      >
        <IconMenu />
      </button>

      <div className="topbar__titles">
        <h1 className="topbar__title">{title}</h1>
        <p className="topbar__sub">{subtitle}</p>
      </div>

      <div className="topbar__actions">
        <div className="search topbar__search">
          <IconSearch />
          <input
            ref={inputRef}
            className="input"
            type="search"
            value={query}
            placeholder="搜索标题、描述、设备或区域"
            aria-label="搜索问题"
            onChange={(event) => onQueryChange(event.target.value)}
          />
          {query === '' ? <span className="search__hint">Ctrl K</span> : null}
        </div>

        <button
          type="button"
          className={`icon-btn${refreshing ? ' icon-btn--busy' : ''}`}
          aria-label="刷新问题列表"
          title="刷新问题列表"
          onClick={onRefresh}
          disabled={refreshing}
        >
          <IconRefresh />
        </button>
      </div>
    </header>
  );
}
