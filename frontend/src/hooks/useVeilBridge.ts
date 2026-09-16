import { useEffect } from 'react';

export interface ChatEntry {
  id: number;
  role: 'user' | 'ai';
  userText: string | null;
  answer: string;
  timestamp: string;
  image: string | null;
}

export interface ChatSummary {
  id: string;
  title: string | null;
  timestamp: string;
}

export type ChatMessage =
  | { type: 'chats.loaded'; chats: ChatSummary[] }
  | { type: 'chat.loaded'; chatId: string; entries: ChatEntry[] }
  | { type: 'chat.added'; entry: ChatEntry }
  | { type: 'chat.error'; message: string }
  | {
      type: 'settings.apiKeyStatus';
      configured: boolean;
      model: string;
      promptInstructions: string;
      maxRecentMessages: number;
      answerLength: 'Short' | 'Balanced' | 'Advanced';
    }
  | { type: 'settings.apiKeySaved'; success: boolean; message?: string };

interface VeilBridgeState {
  onMessage: (message: ChatMessage) => void;
}

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

export const sendMessage = (message: unknown): void => {
  window.chrome?.webview?.postMessage(message);
};

export const useVeilBridge = ({ onMessage }: VeilBridgeState): void => {
  useEffect(() => {
    window.veilChat = { receive: onMessage };
    sendMessage({ type: 'chat.ready' });

    return (): void => {
      delete window.veilChat;
    };
  }, [onMessage]);
};
