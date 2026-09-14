import { ImagePlus, Send, X } from 'lucide-react';
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
  | { type: 'settings.apiKeyStatus'; configured: boolean }
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

function sendMessage(message: unknown): void {
  window.chrome?.webview?.postMessage(message);
}

export function App(): ReactNode {
  const [entries, setEntries] = useState<ChatEntry[]>([]);
  const [text, setText] = useState('');
  const [pendingImage, setPendingImage] = useState<string | null>(null);
  const [apiKey, setApiKey] = useState('');
  const [model, setModel] = useState('gpt-4o-mini');
  const [apiKeyConfigured, setApiKeyConfigured] = useState(true);
  const [apiKeyMessage, setApiKeyMessage] = useState('');
  const imageInputRef = useRef<HTMLInputElement>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    window.veilChat = {
      receive: (message: ChatMessage): void => {
        if (message.type === 'settings.apiKeyStatus') {
          setApiKeyConfigured(message.configured);
        } else if (message.type === 'settings.apiKeySaved') {
          if (message.success) {
            setApiKey('');
            setApiKeyConfigured(true);
            setApiKeyMessage('API key saved securely.');
          } else {
            setApiKeyMessage(message.message ?? 'Could not save the API key.');
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

  function submit(): void {
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
  }

  function saveApiKey(): void {
    if (!apiKey.trim()) {
      setApiKeyMessage('Enter an API key.');
      return;
    }
    setApiKeyMessage('Saving...');
    sendMessage({
      type: 'settings.saveApiKey',
      apiKey: apiKey.trim(),
      model: model.trim(),
    });
  }

  function handleSubmit(event: SubmitEvent<HTMLFormElement>): void {
    event.preventDefault();
    submit();
  }

  function handleKeyDown(event: KeyboardEvent<HTMLTextAreaElement>): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      submit();
    }
  }

  function handleImageSelected(event: ChangeEvent<HTMLInputElement>): void {
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
  }

  function removePendingImage(): void {
    setPendingImage(null);
    if (imageInputRef.current) {
      imageInputRef.current.value = '';
    }
  }

  return (
    <main className="app-shell">
      <header className="app-header">
        <p className="eyebrow">Desktop workspace</p>
        <h1>Veil</h1>
      </header>

      {!apiKeyConfigured && (
        <section className="settings-panel">
          <h2>Connect OpenAI</h2>
          <p>
            Enter your OpenAI API key once. Veil stores it securely on this
            Windows account.
          </p>
          <input
            aria-label="OpenAI API key"
            type="password"
            placeholder="sk-..."
            value={apiKey}
            onChange={(event): void => setApiKey(event.target.value)}
          />
          <input
            aria-label="OpenAI model"
            type="text"
            placeholder="gpt-4o-mini"
            value={model}
            onChange={(event): void => setModel(event.target.value)}
          />
          <button type="button" onClick={saveApiKey}>
            Save API key
          </button>
          {apiKeyMessage && <p>{apiKeyMessage}</p>}
        </section>
      )}

      <section className="chat-container" aria-live="polite">
        {entries.length === 0 ? (
          <p className="empty-state">Enter your first message below.</p>
        ) : (
          entries.map((entry) => (
            <article className="chat-entry" key={entry.id}>
              {entry.userText && <p className="chat-text">{entry.userText}</p>}
              {entry.answer && <p className="chat-text">{entry.answer}</p>}
              {entry.image && (
                <img className="chat-image" src={entry.image} alt="Attached" />
              )}
              <time dateTime={entry.timestamp}>
                {new Date(entry.timestamp).toLocaleString()}
              </time>
            </article>
          ))
        )}
        <div ref={messagesEndRef} />
      </section>

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
    </main>
  );
}
