import { act, cleanup, render, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { App } from './App';

const postMessage = vi.fn();

const installWebView = (): void => {
  Object.defineProperty(window, 'chrome', {
    configurable: true,
    value: { webview: { postMessage } },
  });
};

const sendBackendMessage = async (
  message: Parameters<NonNullable<typeof window.veilChat>['receive']>[0]
): Promise<void> => {
  await waitFor(() => expect(window.veilChat).toBeDefined());
  act(() => {
    window.veilChat?.receive(message);
  });
};

describe('App interactions', () => {
  afterEach(() => {
    cleanup();
    delete window.veilChat;
    delete window.chrome;
  });

  beforeEach(() => {
    cleanup();
    postMessage.mockReset();
    delete window.veilChat;
    installWebView();
    HTMLElement.prototype.scrollIntoView = vi.fn();
  });

  it('requires an API key and model before saving initial settings', async () => {
    const user = userEvent.setup();
    const view = render(<App />);
    await sendBackendMessage({
      type: 'settings.apiKeyStatus',
      configured: false,
      model: null,
    });

    await user.click(view.getByRole('button', { name: 'Save' }));

    expect(view.getByText('Enter an API key and model.')).toBeInTheDocument();
    expect(postMessage).not.toHaveBeenCalledWith(
      expect.objectContaining({ type: 'settings.saveApiKey' })
    );
  });

  it('submits settings through the WebView bridge', async () => {
    const user = userEvent.setup();
    const view = render(<App />);
    await sendBackendMessage({
      type: 'settings.apiKeyStatus',
      configured: false,
      model: null,
    });

    const apiKeyInput = view.getByLabelText('OpenAI API key');
    const modelInput = view.getByLabelText('OpenAI model');
    await user.type(apiKeyInput, 'sk-test');
    await user.clear(modelInput);
    await user.type(modelInput, 'gpt-test');
    await user.click(view.getByRole('button', { name: 'Save' }));

    expect(postMessage).toHaveBeenCalledWith({
      type: 'settings.saveApiKey',
      apiKey: 'sk-test',
      model: 'gpt-test',
    });
  });

  it('submits a chat message and clears the editor', async () => {
    const user = userEvent.setup();
    const view = render(<App />);
    await sendBackendMessage({
      type: 'settings.apiKeyStatus',
      configured: true,
      model: 'gpt-test',
    });

    const editor = view.getByRole('textbox', { name: 'Chat message' });
    await user.type(editor, 'Hello Veil');
    await user.click(view.getByRole('button', { name: 'Send' }));

    expect(postMessage).toHaveBeenCalledWith({
      type: 'chat.submit',
      text: 'Hello Veil',
      image: null,
    });
    expect(editor).toHaveValue('');
  });

  it('shows the active chat history when startup responses arrive', async () => {
    const view = render(<App />);
    await sendBackendMessage({
      type: 'settings.apiKeyStatus',
      configured: true,
      model: 'gpt-test',
    });
    await sendBackendMessage({
      type: 'chats.loaded',
      chats: [
        {
          id: 'chat-1',
          title: 'Existing chat',
          timestamp: '2026-01-01T00:00:00.000Z',
        },
      ],
    });
    await sendBackendMessage({
      type: 'chat.loaded',
      chatId: 'chat-1',
      entries: [
        {
          id: 1,
          role: 'user',
          userText: 'Previous question',
          answer: '',
          timestamp: '2026-01-01T00:00:00.000Z',
          image: null,
        },
      ],
    });

    expect(view.getByText('Previous question')).toBeInTheDocument();
  });

  it('opens chats and renames a selected chat', async () => {
    const user = userEvent.setup();
    const view = render(<App />);
    await sendBackendMessage({
      type: 'settings.apiKeyStatus',
      configured: true,
      model: 'gpt-test',
    });
    await sendBackendMessage({
      type: 'chats.loaded',
      chats: [
        {
          id: 'chat-1',
          title: 'First chat',
          timestamp: '2026-01-01T00:00:00.000Z',
        },
      ],
    });

    await user.click(view.getByRole('button', { name: 'Open navigation' }));
    await user.click(view.getByRole('button', { name: 'Chats' }));
    await user.click(view.getByRole('button', { name: 'Rename chat' }));
    const titleEditor = view.getByDisplayValue('First chat');
    await user.clear(titleEditor);
    await user.type(titleEditor, 'Renamed chat');
    await user.click(view.getByRole('button', { name: 'Save chat name' }));

    expect(postMessage).toHaveBeenCalledWith({
      type: 'chat.rename',
      chatId: 'chat-1',
      title: 'Renamed chat',
    });
  });

  it('starts a new chat and closes the chats modal', async () => {
    const user = userEvent.setup();
    const view = render(<App />);
    await sendBackendMessage({
      type: 'settings.apiKeyStatus',
      configured: true,
      model: 'gpt-test',
    });

    await user.click(view.getByRole('button', { name: 'Open navigation' }));
    await user.click(view.getByRole('button', { name: 'Chats' }));
    await user.click(view.getByRole('button', { name: 'Start new chat' }));

    expect(postMessage).toHaveBeenCalledWith({ type: 'chat.list' });
    expect(postMessage).toHaveBeenCalledWith({ type: 'chat.new' });
    expect(
      view.queryByRole('heading', { name: 'Chats' })
    ).not.toBeInTheDocument();
  });

  it('opens and deletes a chat from the chats modal', async () => {
    const user = userEvent.setup();
    const view = render(<App />);
    await sendBackendMessage({
      type: 'settings.apiKeyStatus',
      configured: true,
      model: 'gpt-test',
    });
    await sendBackendMessage({
      type: 'chats.loaded',
      chats: [
        {
          id: 'chat-1',
          title: 'First chat',
          timestamp: '2026-01-01T00:00:00.000Z',
        },
      ],
    });

    await user.click(view.getByRole('button', { name: 'Open navigation' }));
    await user.click(view.getByRole('button', { name: 'Chats' }));
    await user.click(view.getByRole('button', { name: 'First chat' }));
    expect(postMessage).toHaveBeenCalledWith({
      type: 'chat.open',
      chatId: 'chat-1',
    });

    await user.click(view.getByRole('button', { name: 'Open navigation' }));
    await user.click(view.getByRole('button', { name: 'Chats' }));
    await user.click(view.getByRole('button', { name: 'Delete chat' }));
    expect(postMessage).toHaveBeenCalledWith({
      type: 'chat.delete',
      chatId: 'chat-1',
    });
  });

  it('closes the sidebar and settings modal', async () => {
    const user = userEvent.setup();
    const view = render(<App />);
    await sendBackendMessage({
      type: 'settings.apiKeyStatus',
      configured: true,
      model: 'gpt-test',
    });

    await user.click(view.getByRole('button', { name: 'Open navigation' }));
    await user.click(
      within(view.getByRole('complementary')).getByRole('button', {
        name: 'Close navigation',
      })
    );
    expect(
      view.queryByRole('button', { name: 'Chats' })
    ).not.toBeInTheDocument();

    await user.click(view.getByRole('button', { name: 'Open settings' }));
    await user.click(view.getByRole('button', { name: 'Cancel' }));
    expect(
      view.queryByRole('heading', { name: 'Settings' })
    ).not.toBeInTheDocument();
  });

  it('submits with Enter but preserves a newline with Shift+Enter', async () => {
    const user = userEvent.setup();
    const view = render(<App />);
    await sendBackendMessage({
      type: 'settings.apiKeyStatus',
      configured: true,
      model: 'gpt-test',
    });
    const editor = view.getByRole('textbox', { name: 'Chat message' });

    await user.type(editor, 'first');
    await user.keyboard('{Shift>}{Enter}{/Shift}second');
    expect(editor).toHaveValue('first\nsecond');
    expect(postMessage).not.toHaveBeenCalledWith(
      expect.objectContaining({ type: 'chat.submit' })
    );

    await user.keyboard('{Enter}');
    expect(postMessage).toHaveBeenCalledWith({
      type: 'chat.submit',
      text: 'first\nsecond',
      image: null,
    });
  });

  it('does not submit whitespace-only messages', async () => {
    const user = userEvent.setup();
    const view = render(<App />);
    await sendBackendMessage({
      type: 'settings.apiKeyStatus',
      configured: true,
      model: 'gpt-test',
    });
    const editor = view.getByRole('textbox', { name: 'Chat message' });

    await user.type(editor, '   ');
    await user.keyboard('{Enter}');

    expect(postMessage).not.toHaveBeenCalledWith(
      expect.objectContaining({ type: 'chat.submit' })
    );
    expect(editor).toHaveValue('   ');
  });

  it('selects and removes an image before submitting it', async () => {
    const user = userEvent.setup();
    const view = render(<App />);
    await sendBackendMessage({
      type: 'settings.apiKeyStatus',
      configured: true,
      model: 'gpt-test',
    });
    const file = new File(['image'], 'image.png', { type: 'image/png' });
    const readAsDataURL = vi
      .spyOn(FileReader.prototype, 'readAsDataURL')
      .mockImplementation(function (this: FileReader) {
        Object.defineProperty(this, 'result', {
          configurable: true,
          value: 'data:image/png;base64,test',
        });
        this.dispatchEvent(new Event('load'));
      });

    await user.click(view.getByRole('button', { name: 'Image' }));
    const fileInput = view.container.querySelector('input[type="file"]');
    expect(fileInput).toBeInTheDocument();
    await user.upload(fileInput as HTMLInputElement, file);

    expect(
      view.getByRole('button', { name: 'Remove selected image' })
    ).toBeInTheDocument();
    await user.click(
      view.getByRole('button', { name: 'Remove selected image' })
    );
    expect(
      view.queryByRole('button', { name: 'Remove selected image' })
    ).not.toBeInTheDocument();
    expect(view.getByRole('button', { name: 'Send' })).toBeDisabled();
    readAsDataURL.mockRestore();
  });

  it('submits an image without text and clears the image state', async () => {
    const user = userEvent.setup();
    const view = render(<App />);
    await sendBackendMessage({
      type: 'settings.apiKeyStatus',
      configured: true,
      model: 'gpt-test',
    });
    const file = new File(['image'], 'image.png', { type: 'image/png' });
    vi.spyOn(FileReader.prototype, 'readAsDataURL').mockImplementation(
      function (this: FileReader) {
        Object.defineProperty(this, 'result', {
          configurable: true,
          value: 'data:image/png;base64,test',
        });
        this.dispatchEvent(new Event('load'));
      }
    );

    const fileInput = view.container.querySelector(
      'input[type="file"]'
    ) as HTMLInputElement;
    await user.upload(fileInput, file);
    await user.click(view.getByRole('button', { name: 'Send' }));

    expect(postMessage).toHaveBeenCalledWith({
      type: 'chat.submit',
      text: null,
      image: 'data:image/png;base64,test',
    });
    expect(
      view.queryByRole('button', { name: 'Remove selected image' })
    ).not.toBeInTheDocument();
    vi.restoreAllMocks();
  });

  it('cancels and validates chat rename without sending invalid messages', async () => {
    const user = userEvent.setup();
    const view = render(<App />);
    await sendBackendMessage({
      type: 'settings.apiKeyStatus',
      configured: true,
      model: 'gpt-test',
    });
    await sendBackendMessage({
      type: 'chats.loaded',
      chats: [
        {
          id: 'chat-1',
          title: 'First chat',
          timestamp: '2026-01-01T00:00:00.000Z',
        },
      ],
    });

    await user.click(view.getByRole('button', { name: 'Open navigation' }));
    await user.click(view.getByRole('button', { name: 'Chats' }));
    await user.click(view.getByRole('button', { name: 'Rename chat' }));
    await user.click(view.getByRole('button', { name: 'Cancel rename' }));
    expect(postMessage).not.toHaveBeenCalledWith(
      expect.objectContaining({ type: 'chat.rename' })
    );

    await user.click(view.getByRole('button', { name: 'Rename chat' }));
    const titleEditor = view.getByDisplayValue('First chat');
    await user.clear(titleEditor);
    await user.click(view.getByRole('button', { name: 'Save chat name' }));
    expect(postMessage).not.toHaveBeenCalledWith(
      expect.objectContaining({ type: 'chat.rename' })
    );
  });

  it('shows settings-save failures and clears settings after success', async () => {
    const user = userEvent.setup();
    const view = render(<App />);
    await sendBackendMessage({
      type: 'settings.apiKeyStatus',
      configured: true,
      model: 'gpt-test',
    });

    await user.click(view.getByRole('button', { name: 'Open settings' }));
    await user.type(view.getByLabelText('OpenAI API key'), 'sk-test');
    await user.click(view.getByRole('button', { name: 'Save' }));
    await sendBackendMessage({
      type: 'settings.apiKeySaved',
      success: false,
      message: 'Invalid API key.',
    });
    expect(view.getByText('Invalid API key.')).toBeInTheDocument();

    await sendBackendMessage({ type: 'settings.apiKeySaved', success: true });
    expect(
      view.queryByRole('heading', { name: 'Settings' })
    ).not.toBeInTheDocument();
  });

  it('renders chat errors received from the bridge', async () => {
    const view = render(<App />);
    await sendBackendMessage({
      type: 'settings.apiKeyStatus',
      configured: true,
      model: 'gpt-test',
    });
    await sendBackendMessage({
      type: 'chat.error',
      message: 'The request failed.',
    });

    expect(view.getByText('Error: The request failed.')).toBeInTheDocument();
  });
});
