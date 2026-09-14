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
  const [settingsConfigured, setSettingsConfigured] = useState(false);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [settingsMessage, setSettingsMessage] = useState('');
  const imageInputRef = useRef<HTMLInputElement>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    window.veilChat = {
      receive: (message: ChatMessage): void => {
        if (message.type === 'settings.apiKeyStatus') {
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
    <main className="app-shell">
      <header className="app-header">
        <p className="eyebrow">Windows desktop AI chat application</p>
        <h1>Veil</h1>
      </header>

      {settingsConfigured && (
        <button
          className="settings-button"
          type="button"
          aria-label="Open settings"
          title="Settings"
          onClick={(): void => setSettingsOpen(true)}
        >
          <Settings size={20} aria-hidden="true" />
        </button>
      )}

      {(!settingsConfigured || settingsOpen) && (
        <div className="settings-overlay">
          <section className="settings-panel">
            <h2>{settingsConfigured ? 'Settings' : 'Connect OpenAI'}</h2>
            {!settingsConfigured && (
              <p>Enter your OpenAI API key and model to continue.</p>
            )}
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
            />
            <input
              aria-label="OpenAI model"
              type="text"
              placeholder="gpt-4o-mini"
              value={model}
              onChange={(event: ChangeEvent<HTMLInputElement>): void =>
                setModel(event.target.value)
              }
            />
            <div className="settings-actions">
              <button type="button" onClick={saveApiKey}>
                Save
              </button>
              {settingsConfigured && (
                <button
                  type="button"
                  className="secondary-button"
                  onClick={(): void => setSettingsOpen(false)}
                >
                  Cancel
                </button>
              )}
            </div>
            {settingsMessage && <p>{settingsMessage}</p>}
          </section>
        </div>
      )}

      {settingsConfigured && (
        <section className="chat-container" aria-live="polite">
          {entries.length === 0 ? (
            <p className="empty-state">Enter your first message below.</p>
          ) : (
            entries.map((entry) => (
              <article className="chat-entry" key={entry.id}>
                {entry.userText && (
                  <p className="chat-text">{entry.userText}</p>
                )}
                {entry.answer && <p className="chat-text">{entry.answer}</p>}
                {entry.image && (
                  <img
                    className="chat-image"
                    src={entry.image}
                    alt="Attached"
                  />
                )}
                <time dateTime={entry.timestamp}>
                  {new Date(entry.timestamp).toLocaleString()}
                </time>
              </article>
            ))
          )}
          <div ref={messagesEndRef} />
        </section>
      )}

      {settingsConfigured && (
        <form className="composer" onSubmit={handleSubmit}>
          <input
            ref={imageInputRef}
            className="image-input"
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
          />
          <div className="composer-actions">
            <div className="image-button-wrapper">
              <button
                className="secondary-button"
                type="button"
                onClick={(): void => imageInputRef.current?.click()}
              >
                <ImagePlus size={18} aria-hidden="true" />
                Image
              </button>
              {pendingImage && (
                <button
                  className="image-status"
                  type="button"
                  aria-label="Remove selected image"
                  onClick={removePendingImage}
                >
                  <X size={14} aria-hidden="true" />
                </button>
              )}
            </div>
            <button type="submit" disabled={!text.trim() && !pendingImage}>
              <Send size={18} aria-hidden="true" />
              Send
            </button>
          </div>
        </form>
      )}
    </main>
  );
};
