import { ImagePlus, Send, Settings, X } from 'lucide-react';
import type { ChangeEvent, KeyboardEvent, ReactNode, SubmitEvent } from 'react';
import { useEffect, useRef, useState } from 'react';

interface ChatEntry {
  id: number;
  userText: string | null;
  answer: string;
  timestamp: string;
  image: string | null;
}

type ChatMessage =
  | { type: 'chat.loaded'; entries: ChatEntry[] }
  | { type: 'chat.added'; entry: ChatEntry }
  | { type: 'chat.error'; message: string }
  | {
      type: 'settings.apiKeyStatus';
      configured: boolean;
      model?: string | null;
    }
  | { type: 'settings.apiKeySaved'; success: boolean; message?: string };

declare global {
  interface Window {
    chrome?: {
      webview?: {
        postMessage: (message: unknown) => void;
      };
    };
    veilChat?: {
      receive: (message: ChatMessage) => void;
    };
  }
}

const sendMessage = (message: unknown): void => {
  window.chrome?.webview?.postMessage(message);
};

export const App = (): ReactNode => {
  const [entries, setEntries] = useState<ChatEntry[]>([]);
  const [text, setText] = useState('');
  const [pendingImage, setPendingImage] = useState<string | null>(null);
  const [apiKey, setApiKey] = useState('');
  const [model, setModel] = useState('gpt-4o-mini');
  const [settingsLoaded, setSettingsLoaded] = useState(false);
  const [settingsConfigured, setSettingsConfigured] = useState(false);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [settingsMessage, setSettingsMessage] = useState('');
  const imageInputRef = useRef<HTMLInputElement>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    window.veilChat = {
      receive: (message: ChatMessage): void => {
        if (message.type === 'settings.apiKeyStatus') {
          setSettingsLoaded(true);
          setSettingsConfigured(message.configured);
          if (message.model) {
            setModel(message.model);
          }
        } else if (message.type === 'settings.apiKeySaved') {
          if (message.success) {
            setApiKey('');
            setSettingsConfigured(true);
            setSettingsOpen(false);
            setSettingsMessage('');
          } else {
            setSettingsMessage(
              message.message ?? 'Could not save the settings.'
            );
          }
        } else if (message.type === 'chat.loaded') {
          setEntries(message.entries);
        } else if (message.type === 'chat.added') {
          setEntries((currentEntries) => [...currentEntries, message.entry]);
        } else {
          setEntries((currentEntries) => [
            ...currentEntries,
            {
              id: Date.now(),
              userText: null,
              answer: `Error: ${message.message}`,
              timestamp: new Date().toISOString(),
              image: null,
            },
          ]);
        }
      },
    };

    sendMessage({ type: 'chat.ready' });

    return (): void => {
      delete window.veilChat;
    };
  }, []);

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
      <header className="shrink-0">
        <p className="mb-2 text-xs font-bold uppercase tracking-[0.12em] text-[#7dd3fc]">
          Windows desktop AI chat application
        </p>
        <h1 className="m-0 text-3xl leading-[0.95]">Veil</h1>
      </header>

      {settingsLoaded && settingsConfigured && (
        <button
          className="absolute right-6 top-6 flex min-h-11 min-w-11 items-center justify-center rounded-lg border-0 bg-[#7dd3fc] p-0 font-[inherit] font-bold text-[#081018] shadow-none hover:bg-[#7dd3fc]"
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
              <article
                className="mb-3 rounded-xl border border-[#273542] bg-[#19232d] px-4 py-[0.85rem]"
                key={entry.id}
              >
                {entry.userText && (
                  <p className="mb-2 whitespace-pre-wrap break-words">
                    {entry.userText}
                  </p>
                )}
                {entry.answer && (
                  <p className="mb-2 whitespace-pre-wrap break-words">
                    {entry.answer}
                  </p>
                )}
                {entry.image && (
                  <img
                    className="mb-2 block max-h-80 max-w-[32rem] rounded-lg object-contain"
                    src={entry.image}
                    alt="Attached"
                  />
                )}
                <time
                  className="text-xs text-[#7d8a97]"
                  dateTime={entry.timestamp}
                >
                  {new Date(entry.timestamp).toLocaleString()}
                </time>
              </article>
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
