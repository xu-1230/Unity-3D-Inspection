/**
 * 后端基址。留空表示走同源相对路径，由 Vite 的 /api 代理转发到后端；
 * 需要直连非本机地址时通过 VITE_API_BASE_URL 覆盖。
 */
export const API_BASE_URL: string = import.meta.env.VITE_API_BASE_URL ?? '';

/** 单次请求的兜底超时，避免后端无响应时页面一直停在加载态。 */
const REQUEST_TIMEOUT_MS = 10_000;

interface ErrorBody {
  error?: { code?: string; message?: string; field?: string };
}

/** 供错误提示展示的接口地址，不含具体标识，避免把长 id 塞进提示文案。 */
export const API_DISPLAY_TARGET = API_BASE_URL === '' ? '/api/issues' : `${API_BASE_URL}/api/issues`;

function networkMessage(): string {
  return '无法连接到后端服务，请确认 Go 服务已在 8080 端口启动';
}

/**
 * 统一的后端调用入口：拼接基址、携带 JSON 头、超时中断，
 * 并把网络错误、HTTP 错误、非法 JSON 统一转成可直接展示的中文文案。
 */
export async function apiRequest<T>(path: string, init: RequestInit = {}): Promise<T> {
  const controller = new AbortController();
  const timer = window.setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS);

  try {
    const response = await fetch(`${API_BASE_URL}${path}`, {
      ...init,
      signal: controller.signal,
      headers: { 'Content-Type': 'application/json', ...(init.headers ?? {}) },
    });

    if (!response.ok) {
      let message = '';
      try {
        const body = (await response.json()) as ErrorBody;
        if (body.error?.message) {
          message = body.error.message;
        }
      } catch {
        // 响应不是合法 JSON（例如开发代理在后端离线时返回的 500）
      }

      if (message === '') {
        message =
          response.status >= 500
            ? `后端服务不可用（HTTP ${response.status}），请确认 Go 服务已在 8080 端口启动`
            : `请求失败（HTTP ${response.status}）`;
      }
      throw new Error(message);
    }

    if (response.status === 204) {
      return undefined as T;
    }

    const text = await response.text();
    if (text.trim() === '') {
      return undefined as T;
    }

    try {
      return JSON.parse(text) as T;
    } catch {
      throw new Error('后端返回的不是合法 JSON，请检查服务版本');
    }
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw new Error('请求超时，后端未在 10 秒内响应');
    }
    if (error instanceof TypeError) {
      throw new Error(networkMessage());
    }
    throw error;
  } finally {
    window.clearTimeout(timer);
  }
}
