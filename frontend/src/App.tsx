import {
  Check,
  ImagePlus,
  Menu,
  MessageSquare,
  Pencil,
  Send,
  Settings,
  Trash2,
  X,
} from 'lucide-react';
import type { ChangeEvent, KeyboardEvent, ReactNode, SubmitEvent } from 'react';
import { useCallback, useEffect, useRef, useState } from 'react';
import { ChatMessage as ChatMessageView } from './components/ChatMessage';
import { useChat } from './hooks/useChat';
import { sendMessage } from './hooks/useVeilBridge';

export const App = (): ReactNode => {
  const [text, setText] = useState('');
  const [pendingImage, setPendingImage] = useState<string | null>(null);
  const [apiKey, setApiKey] = useState('');
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [chatsOpen, setChatsOpen] = useState(false);
  const [editingChatId, setEditingChatId] = useState<string | null>(null);
  const [editingTitle, setEditingTitle] = useState('');

  const clearApiKey = useCallback((): void => setApiKey(''), []);
  const {
    activeChatId,
    chats,
    entries,
    model,
    settingsConfigured,
    settingsLoaded,
    settingsMessage,
    settingsOpen,
    setModel,
    setSettingsMessage,
    setSettingsOpen,
  } = useChat({ onApiKeySaved: clearApiKey });

  const saveChatTitle = (chatId: string): void => {
    const title = editingTitle.trim();
    if (title) {
      sendMessage({ type: 'chat.rename', chatId, title });
    }
    setEditingChatId(null);
  };
  const imageInputRef = useRef<HTMLInputElement>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [entries]);

  const submit = (): void => {
    const trimmedText = text.trim();
    if (!trimmedText && !pendingImage) {
      return;
    }

    sendMessage({
      type: 'chat.submit',
      text: trimmedText || null,
      image: pendingImage,
    });
    setText('');
    setPendingImage(null);
    if (imageInputRef.current) {
      imageInputRef.current.value = '';
    }
  };

  const saveApiKey = (): void => {
    if ((!settingsConfigured && !apiKey.trim()) || !model.trim()) {
      setSettingsMessage('Enter an API key and model.');
      return;
    }
    setSettingsMessage('Saving...');
    sendMessage({
      type: 'settings.saveApiKey',
      apiKey: apiKey.trim(),
      model: model.trim(),
    });
  };

  const handleSubmit = (event: SubmitEvent<HTMLFormElement>): void => {
    event.preventDefault();
    submit();
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>): void => {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      submit();
    }
  };

  const handleImageSelected = (event: ChangeEvent<HTMLInputElement>): void => {
    const file = event.target.files?.[0];
    if (!file) {
      return;
    }

    const reader = new FileReader();
    reader.addEventListener('load', () => {
      if (typeof reader.result === 'string') {
        setPendingImage(reader.result);
      }
    });
    reader.readAsDataURL(file);
  };

  const removePendingImage = (): void => {
    setPendingImage(null);
    if (imageInputRef.current) {
      imageInputRef.current.value = '';
    }
  };

  return (
    <main className="relative flex h-screen flex-col gap-4 p-6">
      {settingsLoaded && (
        <>
          <button
            className="absolute right-6 top-6 z-10 inline-flex min-h-11 min-w-11 items-center justify-center rounded-lg border-0 bg-[#273542] p-0 text-[#e8edf2]"
            type="button"
            aria-label={sidebarOpen ? 'Close navigation' : 'Open navigation'}
            title={sidebarOpen ? 'Close navigation' : 'Open navigation'}
            onClick={(): void => setSidebarOpen((open) => !open)}
          >
            <Menu size={20} aria-hidden="true" />
          </button>
          {sidebarOpen && (
            <aside className="absolute inset-y-0 right-0 z-20 flex w-64 flex-col border-l border-[#3a4b5a] bg-[#19232d] p-6 shadow-[0_1.5rem_4rem_rgb(0_0_0_/_45%)]">
              <button
                className="mb-8 inline-flex min-h-11 min-w-11 self-start items-center justify-center rounded-lg border-0 bg-[#273542] p-0 text-[#e8edf2]"
                type="button"
                aria-label="Close navigation"
                title="Close navigation"
                onClick={(): void => setSidebarOpen(false)}
              >
                <X size={20} aria-hidden="true" />
              </button>
              <button
                className="inline-flex min-h-11 w-full items-center justify-start gap-3 rounded-lg border-0 bg-[#273542] px-4 font-[inherit] font-bold text-[#e8edf2]"
                type="button"
                onClick={(): void => {
                  setSidebarOpen(false);
                  sendMessage({ type: 'chat.list' });
                  setChatsOpen(true);
                }}
              >
                <MessageSquare size={20} aria-hidden="true" />
                Chats
              </button>
              <button
                className="mt-3 inline-flex min-h-11 w-full items-center justify-start gap-3 rounded-lg border-0 bg-[#273542] px-4 font-[inherit] font-bold text-[#e8edf2]"
                type="button"
                onClick={(): void => {
                  setSidebarOpen(false);
                  setSettingsOpen(true);
                }}
              >
                <Settings size={20} aria-hidden="true" />
                Settings
              </button>
            </aside>
          )}

          {settingsLoaded && chatsOpen && (
            <div className="fixed inset-0 z-10 flex items-center justify-center bg-black/[62%] p-6">
              <section className="flex w-full max-w-[34rem] flex-col gap-6 rounded-2xl border border-[#3a4b5a] bg-[#19232d] px-9 py-8 shadow-[0_1.5rem_4rem_rgb(0_0_0_/_45%)]">
                <div className="flex items-center justify-between">
                  <h2 className="m-0 text-2xl">Chats</h2>
                  <button
                    className="inline-flex min-h-11 min-w-11 items-center justify-center rounded-lg border-0 bg-[#273542] p-0 text-[#e8edf2]"
                    type="button"
                    aria-label="Close chats"
                    onClick={(): void => setChatsOpen(false)}
                  >
                    <X size={20} aria-hidden="true" />
                  </button>
                </div>
                <div className="flex max-h-80 flex-col gap-2 overflow-y-auto">
                  {chats.map((chat) => (
                    <div className="flex items-center gap-2" key={chat.id}>
                      {editingChatId === chat.id ? (
                        <input
                          className="min-h-11 min-w-0 flex-1 rounded-lg border border-[#7dd3fc] bg-[#273542] px-4 font-[inherit] font-bold text-[#e8edf2] outline-none"
                          value={editingTitle}
                          autoFocus
                          onChange={(
                            event: ChangeEvent<HTMLInputElement>
                          ): void => setEditingTitle(event.target.value)}
                          onKeyDown={(
                            event: KeyboardEvent<HTMLInputElement>
                          ): void => {
                            if (event.key === 'Enter') {
                              event.preventDefault();
                              saveChatTitle(chat.id);
                            }
                            if (event.key === 'Escape') {
                              setEditingChatId(null);
                            }
                          }}
                        />
                      ) : (
                        <button
                          className={`flex min-h-11 flex-1 items-center justify-between rounded-lg border-0 px-4 text-left font-[inherit] font-bold text-[#e8edf2] ${activeChatId === chat.id ? 'bg-[#166534] text-white' : 'bg-[#273542]'}`}
                          type="button"
                          onClick={(): void => {
                            sendMessage({ type: 'chat.open', chatId: chat.id });
                            setChatsOpen(false);
                          }}
                        >
                          {chat.title ??
                            new Date(chat.timestamp).toLocaleString()}
                        </button>
                      )}
                      {editingChatId !== chat.id && (
                        <button
                          className="inline-flex min-h-11 min-w-11 items-center justify-center rounded-lg border-0 bg-[#273542] p-0 text-[#e8edf2]"
                          type="button"
                          aria-label="Rename chat"
                          onClick={(): void => {
                            setEditingChatId(chat.id);
                            setEditingTitle(chat.title ?? '');
                          }}
                        >
                          <Pencil size={18} aria-hidden="true" />
                        </button>
                      )}
                      {editingChatId === chat.id && (
                        <button
                          className="inline-flex min-h-11 min-w-11 items-center justify-center rounded-lg border-0 bg-[#7dd3fc] p-0 text-[#081018]"
                          type="button"
                          aria-label="Save chat name"
                          onClick={(): void => saveChatTitle(chat.id)}
                        >
                          <Check size={18} aria-hidden="true" />
                        </button>
                      )}
                      {editingChatId === chat.id ? (
                        <button
                          className="inline-flex min-h-11 min-w-11 items-center justify-center rounded-lg border-0 bg-[#273542] p-0 text-[#e8edf2]"
                          type="button"
                          aria-label="Cancel rename"
                          onClick={(): void => setEditingChatId(null)}
                        >
                          <X size={18} aria-hidden="true" />
                        </button>
                      ) : (
                        <button
                          className="inline-flex min-h-11 min-w-11 items-center justify-center rounded-lg border-0 bg-[#ef4444] p-0 text-white"
                          type="button"
                          aria-label="Delete chat"
                          onClick={(): void =>
                            sendMessage({
                              type: 'chat.delete',
                              chatId: chat.id,
                            })
                          }
                        >
                          <Trash2 size={18} aria-hidden="true" />
                        </button>
                      )}
                    </div>
                  ))}
                </div>
                <button
                  className="flex min-h-11 items-center justify-center rounded-lg border-0 bg-[#7dd3fc] px-4 font-[inherit] font-bold text-[#081018]"
                  type="button"
                  onClick={(): void => {
                    sendMessage({ type: 'chat.new' });
                    setChatsOpen(false);
                  }}
                >
                  Start new chat
                </button>
              </section>
            </div>
          )}
        </>
      )}
      <header className="shrink-0">
        <p className="mb-2 text-xs font-bold uppercase tracking-[0.12em] text-[#7dd3fc]">
          Windows desktop AI chat application
        </p>
        <h1 className="m-0 text-3xl leading-[0.95]">Veil</h1>
      </header>

      {settingsLoaded && settingsConfigured && (
        <button
          className="absolute right-20 top-6 flex min-h-11 min-w-11 items-center justify-center rounded-lg border-0 bg-[#7dd3fc] p-0 font-[inherit] font-bold text-[#081018] shadow-none hover:bg-[#7dd3fc]"
          type="button"
          aria-label="Open settings"
          title="Settings"
          onClick={(): void => setSettingsOpen(true)}
        >
          <Settings size={20} aria-hidden="true" />
        </button>
      )}

      {settingsLoaded && (!settingsConfigured || settingsOpen) && (
        <div className="fixed inset-0 z-10 flex items-center justify-center bg-black/[62%] p-6">
          <section className="flex w-full max-w-[34rem] flex-col gap-6 rounded-2xl border border-[#3a4b5a] bg-[#19232d] px-9 py-8 shadow-[0_1.5rem_4rem_rgb(0_0_0_/_45%)]">
            <div className="flex flex-col gap-2">
              <h2 className="m-0 text-2xl">
                {settingsConfigured ? 'Settings' : 'Connect OpenAI'}
              </h2>
              {!settingsConfigured && (
                <p className="m-0 leading-6 text-[#aab8c5]">
                  Enter your OpenAI API key and model to continue.
                </p>
              )}
            </div>
            <div className="flex flex-col gap-4">
              <label className="flex flex-col gap-2 text-sm font-bold text-[#c8d3dc]">
                API key
                <input
                  aria-label="OpenAI API key"
                  type="password"
                  placeholder={
                    settingsConfigured
                      ? 'Leave blank to keep current key'
                      : 'sk-...'
                  }
                  value={apiKey}
                  onChange={(event: ChangeEvent<HTMLInputElement>): void =>
                    setApiKey(event.target.value)
                  }
                  className="rounded-lg border border-[#3a4b5a] bg-[#19232d] p-3 text-[#e8edf2] placeholder:text-[#666]"
                />
              </label>
              <label className="flex flex-col gap-2 text-sm font-bold text-[#c8d3dc]">
                Model
                <input
                  aria-label="OpenAI model"
                  type="text"
                  placeholder="gpt-4o-mini"
                  value={model}
                  onChange={(event: ChangeEvent<HTMLInputElement>): void =>
                    setModel(event.target.value)
                  }
                  className="rounded-lg border border-[#3a4b5a] bg-[#19232d] p-3 text-[#e8edf2] placeholder:text-[#666]"
                />
              </label>
            </div>
            <div className="flex justify-end gap-3">
              <button
                type="button"
                onClick={saveApiKey}
                className="flex h-10 min-h-[2.5rem] items-center justify-center gap-2 rounded-lg border-0 bg-[#7dd3fc] px-3 font-[inherit] font-bold text-[#081018] shadow-none hover:bg-[#7dd3fc]"
              >
                Save
              </button>
              {settingsConfigured && (
                <button
                  type="button"
                  className="flex h-10 min-h-[2.5rem] items-center justify-center gap-2 rounded-lg border-0 bg-[#273542] px-3 font-[inherit] font-bold text-[#e8edf2] shadow-none hover:bg-[#273542]"
                  onClick={(): void => setSettingsOpen(false)}
                >
                  Cancel
                </button>
              )}
            </div>
            {settingsMessage && (
              <p className="m-0 leading-6 text-[#aab8c5]">{settingsMessage}</p>
            )}
          </section>
        </div>
      )}

      {settingsConfigured && (
        <section
          className="min-h-0 flex-1 overflow-y-auto scroll-smooth p-1"
          aria-live="polite"
        >
          {entries.length === 0 ? (
            <p className="text-center text-[#7d8a97]">
              Enter your first message below.
            </p>
          ) : (
            entries.map((entry) => (
              <ChatMessageView key={entry.id} entry={entry} />
            ))
          )}
          <div ref={messagesEndRef} />
        </section>
      )}

      {settingsConfigured && (
        <form className="flex shrink-0 flex-wrap gap-3" onSubmit={handleSubmit}>
          <input
            ref={imageInputRef}
            className="hidden"
            type="file"
            accept="image/jpeg,image/png,image/gif,image/webp"
            onChange={handleImageSelected}
          />
          <textarea
            aria-label="Chat message"
            placeholder="Write a message..."
            value={text}
            onChange={(event: ChangeEvent<HTMLTextAreaElement>): void =>
              setText(event.target.value)
            }
            onKeyDown={handleKeyDown}
            rows={3}
            className="min-h-16 flex-1 resize-y rounded-lg border border-[#3a4b5a] bg-[#19232d] p-3 font-[inherit] text-[#e8edf2] placeholder:text-[#666]"
          />
          <div className="flex items-end gap-2">
            <div className="relative">
              <button
                className="inline-flex min-h-11 min-w-20 items-center justify-center gap-2 self-end rounded-lg border-0 bg-[#273542] px-3 font-[inherit] font-bold text-[#e8edf2] shadow-none hover:bg-[#273542]"
                type="button"
                onClick={(): void => imageInputRef.current?.click()}
              >
                <ImagePlus size={18} aria-hidden="true" />
                Image
              </button>
              {pendingImage && (
                <button
                  className="absolute -right-1.5 -top-1.5 inline-flex min-h-5 min-w-5 items-center justify-center rounded-full border-2 border-[#19232d] bg-[#ef4444] p-0 text-sm leading-none text-white"
                  type="button"
                  aria-label="Remove selected image"
                  onClick={removePendingImage}
                >
                  <X size={14} aria-hidden="true" />
                </button>
              )}
            </div>
            <button
              type="submit"
              className="inline-flex min-h-11 min-w-20 items-center justify-center gap-2 self-end rounded-lg border-0 bg-[#7dd3fc] px-3 font-[inherit] font-bold text-[#081018] disabled:cursor-not-allowed disabled:opacity-45"
              disabled={!text.trim() && !pendingImage}
            >
              <Send size={18} aria-hidden="true" />
              Send
            </button>
          </div>
        </form>
      )}
    </main>
  );
};
