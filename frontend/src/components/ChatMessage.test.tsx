import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { type ChatEntry, ChatMessage } from './ChatMessage';

const baseEntry: ChatEntry = {
  id: 1,
  role: 'ai',
  userText: null,
  answer: '',
  timestamp: '2026-01-01T00:00:00.000Z',
  image: null,
};

describe('ChatMessage', () => {
  it('renders user text and preserves whitespace', () => {
    render(
      <ChatMessage
        entry={{ ...baseEntry, role: 'user', userText: 'Hello\nworld' }}
      />
    );

    expect(screen.getByText(/Hello/).textContent).toBe('Hello\nworld');
  });

  it('renders Markdown content and code blocks', () => {
    render(
      <ChatMessage
        entry={{
          ...baseEntry,
          answer: '**Answer**\n\n```javascript\nconst value = 1;\n```',
        }}
      />
    );

    expect(screen.getByText('Answer')).toBeInTheDocument();
    expect(screen.getByRole('code')).toHaveTextContent('const value = 1;');
  });

  it('renders attached images with accessible alternative text', () => {
    render(
      <ChatMessage
        entry={{ ...baseEntry, image: 'data:image/png;base64,AA==' }}
      />
    );

    expect(screen.getByRole('img', { name: 'Attached' })).toHaveAttribute(
      'src',
      'data:image/png;base64,AA=='
    );
  });
});
