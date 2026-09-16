import { renderHook } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { sendMessage, useVeilBridge } from './useVeilBridge';

const postMessage = vi.fn();

const installWebView = (): void => {
  Object.defineProperty(window, 'chrome', {
    configurable: true,
    value: { webview: { postMessage } },
  });
};

describe('useVeilBridge', () => {
  afterEach(() => {
    postMessage.mockReset();
    delete window.veilChat;
    delete window.chrome;
  });

  it('registers the receiver and announces readiness', () => {
    installWebView();
    const onMessage = vi.fn();

    const { unmount } = renderHook(() => useVeilBridge({ onMessage }));

    expect(window.veilChat?.receive).toBe(onMessage);
    expect(postMessage).toHaveBeenCalledWith({ type: 'chat.ready' });

    unmount();
    expect(window.veilChat).toBeUndefined();
  });

  it('forwards messages through the WebView bridge', () => {
    installWebView();
    const message = { type: 'chat.new' };

    sendMessage(message);

    expect(postMessage).toHaveBeenCalledWith(message);
  });
});
