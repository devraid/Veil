import { useEffect, useRef, useState } from 'react'
import type { ChangeEvent, KeyboardEvent, ReactNode, SubmitEvent } from 'react'
import { ImagePlus, Send } from 'lucide-react'

interface ChatEntry {
  id: number
  userText: string | null
  answer: string
  timestamp: string
  image: string | null
}

type ChatMessage =
  | { type: 'chat.loaded'; entries: ChatEntry[] }
  | { type: 'chat.added'; entry: ChatEntry }

declare global {
  interface Window {
    chrome?: {
      webview?: {
        postMessage: (message: unknown) => void
      }
    }
    veilChat?: {
      receive: (message: ChatMessage) => void
    }
  }
}

function sendMessage(message: unknown): void {
  window.chrome?.webview?.postMessage(message)
}

export function App(): ReactNode {
  const [entries, setEntries] = useState<ChatEntry[]>([])
  const [text, setText] = useState('')
  const [pendingImage, setPendingImage] = useState<string | null>(null)
  const imageInputRef = useRef<HTMLInputElement>(null)
  const messagesEndRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    window.veilChat = {
      receive: (message: ChatMessage): void => {
        if (message.type === 'chat.loaded') {
          setEntries(message.entries)
        } else {
          setEntries((currentEntries) => [...currentEntries, message.entry])
        }
      },
    }

    sendMessage({ type: 'chat.ready' })

    return (): void => {
      delete window.veilChat
    }
  }, [])

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [entries])

  function submit(): void {
    const trimmedText = text.trim()
    if (!trimmedText && !pendingImage) {
      return
    }

    sendMessage({
      type: 'chat.submit',
      text: trimmedText || null,
      image: pendingImage,
    })
    setText('')
    setPendingImage(null)
    if (imageInputRef.current) {
      imageInputRef.current.value = ''
    }
  }

  function handleSubmit(event: SubmitEvent<HTMLFormElement>): void {
    event.preventDefault()
    submit()
  }

  function handleKeyDown(event: KeyboardEvent<HTMLTextAreaElement>): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault()
      submit()
    }
  }

  function handleImageSelected(event: ChangeEvent<HTMLInputElement>): void {
    const file = event.target.files?.[0]
    if (!file) {
      return
    }

    const reader = new FileReader()
    reader.addEventListener('load', () => {
      if (typeof reader.result === 'string') {
        setPendingImage(reader.result)
      }
    })
    reader.readAsDataURL(file)
  }

  return (
    <main className="app-shell">
      <header className="app-header">
        <p className="eyebrow">Desktop workspace</p>
        <h1>Veil</h1>
      </header>

      <section className="chat-container" aria-live="polite">
        {entries.length === 0 ? (
          <p className="empty-state">Enter your first message below.</p>
        ) : (
          entries.map((entry) => (
            <article className="chat-entry" key={entry.id}>
              {entry.userText && <p className="chat-text">{entry.userText}</p>}
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
          onChange={(event: ChangeEvent<HTMLTextAreaElement>): void => setText(event.target.value)}
          onKeyDown={handleKeyDown}
          rows={3}
        />
        <div className="composer-actions">
          <button
            className="secondary-button"
            type="button"
            onClick={(): void => imageInputRef.current?.click()}
          >
            <ImagePlus size={18} aria-hidden="true" />
            Image
          </button>
          <button type="submit" disabled={!text.trim() && !pendingImage}>
            <Send size={18} aria-hidden="true" />
            Send
          </button>
        </div>
        {pendingImage && (
          <img className="image-preview" src={pendingImage} alt="Selected preview" />
        )}
      </form>
    </main>
  )
}
