import type { Dispatch, SetStateAction } from 'react';
import { useCallback, useState } from 'react';
import type { ChatEntry, ChatMessage, ChatSummary } from './useVeilBridge';
import { sendMessage, useVeilBridge } from './useVeilBridge';

interface UseChatOptions {
  onApiKeySaved: () => void;
}

interface UseChatState {
  activeChatId: string | null;
  chats: ChatSummary[];
  entries: ChatEntry[];
  model: string;
  sendMessage: (message: unknown) => void;
  settingsConfigured: boolean;
  settingsLoaded: boolean;
  settingsMessage: string;
  setActiveChatId: Dispatch<SetStateAction<string | null>>;
  setChats: Dispatch<SetStateAction<ChatSummary[]>>;
  setEntries: Dispatch<SetStateAction<ChatEntry[]>>;
  setModel: Dispatch<SetStateAction<string>>;
  setSettingsMessage: Dispatch<SetStateAction<string>>;
  setSettingsOpen: Dispatch<SetStateAction<boolean>>;
  settingsOpen: boolean;
}

export const useChat = ({ onApiKeySaved }: UseChatOptions): UseChatState => {
  const [entries, setEntries] = useState<ChatEntry[]>([]);
  const [chats, setChats] = useState<ChatSummary[]>([]);
  const [activeChatId, setActiveChatId] = useState<string | null>(null);
  const [settingsLoaded, setSettingsLoaded] = useState(false);
  const [settingsConfigured, setSettingsConfigured] = useState(false);
  const [model, setModel] = useState('gpt-4o-mini');
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [settingsMessage, setSettingsMessage] = useState('');

  const handleMessage = useCallback(
    (message: ChatMessage): void => {
      if (message.type === 'settings.apiKeyStatus') {
        setSettingsLoaded(true);
        setSettingsConfigured(message.configured);
        if (message.model) {
          setModel(message.model);
        }
      } else if (message.type === 'settings.apiKeySaved') {
        if (message.success) {
          onApiKeySaved();
          setSettingsConfigured(true);
          setSettingsOpen(false);
          setSettingsMessage('');
        } else {
          setSettingsMessage(message.message ?? 'Could not save the settings.');
        }
      } else if (message.type === 'chats.loaded') {
        setChats(message.chats);
      } else if (message.type === 'chat.loaded') {
        setActiveChatId(message.chatId);
        setEntries(message.entries);
      } else if (message.type === 'chat.added') {
        setEntries((currentEntries) => [...currentEntries, message.entry]);
      } else {
        setEntries((currentEntries) => [
          ...currentEntries,
          {
            id: Date.now(),
            role: 'ai',
            userText: null,
            answer: `Error: ${message.message}`,
            timestamp: new Date().toISOString(),
            image: null,
          },
        ]);
      }
    },
    [onApiKeySaved]
  );

  useVeilBridge({ onMessage: handleMessage });

  return {
    activeChatId,
    chats,
    entries,
    model,
    sendMessage,
    settingsConfigured,
    settingsLoaded,
    settingsMessage,
    setActiveChatId,
    setChats,
    setEntries,
    setModel,
    setSettingsMessage,
    setSettingsOpen,
    settingsOpen,
  };
};
