import { act, renderHook } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useChat } from './useChat';
import type { ChatEntry, ChatMessage } from './useVeilBridge';

const postMessage = vi.fn();

const installWebView = (): void => {
  Object.defineProperty(window, 'chrome', {
    configurable: true,
    value: { webview: { postMessage } },
  });
};

const entry: ChatEntry = {
  id: 1,
  role: 'ai',
  userText: null,
  answer: 'Hello',
  timestamp: '2026-01-01T00:00:00.000Z',
  image: null,
};

describe('useChat', () => {
  afterEach(() => {
    postMessage.mockReset();
    delete window.veilChat;
    delete window.chrome;
  });

  it('updates chat state for loaded and added messages', () => {
    installWebView();
    const onApiKeySaved = vi.fn();
    const { result } = renderHook(() => useChat({ onApiKeySaved }));

    act(() => {
      window.veilChat?.receive({
        type: 'chat.loaded',
        chatId: 'chat-1',
        entries: [entry],
      });
    });
    expect(result.current.activeChatId).toBe('chat-1');
    expect(result.current.entries).toEqual([entry]);

    act(() => {
      window.veilChat?.receive({
        type: 'chat.added',
        entry: { ...entry, id: 2 },
      });
    });
    expect(result.current.entries).toHaveLength(2);
  });

  it('updates settings state and closes settings after a successful save', () => {
    installWebView();
    const onApiKeySaved = vi.fn();
    const { result } = renderHook(() => useChat({ onApiKeySaved }));

    act(() => {
      result.current.setSettingsOpen(true);
      window.veilChat?.receive({
        type: 'settings.apiKeyStatus',
        configured: false,
        model: 'gpt-test',
      });
    });
    expect(result.current.settingsLoaded).toBe(true);
    expect(result.current.model).toBe('gpt-test');

    act(() => {
      window.veilChat?.receive({ type: 'settings.apiKeySaved', success: true });
    });
    expect(onApiKeySaved).toHaveBeenCalledOnce();
    expect(result.current.settingsConfigured).toBe(true);
    expect(result.current.settingsOpen).toBe(false);
  });

  it('renders backend errors as assistant entries', () => {
    installWebView();
    const { result } = renderHook(() => useChat({ onApiKeySaved: vi.fn() }));
    const error: ChatMessage = {
      type: 'chat.error',
      message: 'Request failed',
    };

    act(() => window.veilChat?.receive(error));

    expect(result.current.entries.at(-1)).toMatchObject({
      role: 'ai',
      answer: 'Error: Request failed',
    });
  });
});
