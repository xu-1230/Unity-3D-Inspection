/**
 * 后端基址。留空表示走同源相对路径，由 Vite 的 /api 代理转发到后端；
 * 需要直连非本机地址时通过 VITE_API_BASE_URL 覆盖。
 */
export const API_BASE_URL: string = import.meta.env.VITE_API_BASE_URL ?? '';

interface ErrorBody {
  error?: { code?: string; message?: string; field?: string };
}

/** 统一的后端调用入口：拼接基址、携带 JSON 头、把错误响应转为可展示的文案。 */
export async function apiRequest<T>(path: string, init: RequestInit = {}): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...(init.headers ?? {}) },
  });

  if (!response.ok) {
    let message = `请求失败（HTTP ${response.status}）`;
    try {
      const body = (await response.json()) as ErrorBody;
      if (body.error?.message) {
        message = body.error.message;
      }
    } catch {
      // 响应不是合法 JSON 时保留默认文案
    }
    throw new Error(message);
  }

  return (await response.json()) as T;
}
