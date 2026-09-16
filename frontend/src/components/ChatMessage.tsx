import Markdown from 'react-markdown';
import rehypeHighlight from 'rehype-highlight';
import remarkGfm from 'remark-gfm';
import 'highlight.js/styles/github-dark.css';
import csharp from 'highlight.js/lib/languages/csharp';
import javascript from 'highlight.js/lib/languages/javascript';
import json from 'highlight.js/lib/languages/json';
import python from 'highlight.js/lib/languages/python';
import type { ReactElement } from 'react';

const markdownLanguages: Record<string, unknown> = {
  javascript,
  json,
  python,
  csharp,
};

export interface ChatEntry {
  id: number;
  role: 'user' | 'ai';
  userText: string | null;
  answer: string;
  timestamp: string;
  image: string | null;
}

interface ChatMessageProps {
  entry: ChatEntry;
}

export const ChatMessage = ({ entry }: ChatMessageProps): ReactElement => (
  <article
    className={`mb-3 w-fit max-w-[80%] rounded-xl border border-[#273542] ${entry.role === 'user' ? 'ml-auto bg-[#19232d]' : 'mr-auto bg-[#202d38]'} px-4 py-[0.85rem]`}
  >
    {entry.userText && (
      <p className="mb-2 whitespace-pre-wrap break-words text-right">
        {entry.userText}
      </p>
    )}
    {entry.answer && (
      <div className="mb-2 min-w-0 break-words [&_a]:text-[#7dd3fc] [&_a]:underline [&_blockquote]:border-l-4 [&_blockquote]:border-[#3a4b5a] [&_blockquote]:pl-4 [&_code]:font-mono [&_code]:text-sm [&_h1]:mb-3 [&_h1]:text-xl [&_h2]:mb-3 [&_h2]:text-lg [&_h3]:mb-2 [&_h3]:text-base [&_img]:block [&_img]:max-w-40 [&_img]:rounded-lg [&_img]:object-contain [&_li]:ml-5 [&_li]:list-disc [&_ol]:my-2 [&_ol]:list-decimal [&_p]:mb-3 [&_pre]:my-3 [&_pre]:overflow-x-auto [&_pre]:rounded-lg [&_pre]:bg-[#11161c] [&_pre]:p-4 [&_ul]:my-2 [&_ul]:list-disc">
        <Markdown
          remarkPlugins={[remarkGfm]}
          rehypePlugins={[[rehypeHighlight, { languages: markdownLanguages }]]}
        >
          {entry.answer}
        </Markdown>
      </div>
    )}
    {entry.image && (
      <img
        className="mb-2 block max-h-80 max-w-40 rounded-lg object-contain"
        src={entry.image}
        alt="Attached"
      />
    )}
    <time
      className={`block w-full text-xs text-[#7d8a97] ${entry.role === 'user' ? 'text-right' : 'text-left'}`}
      dateTime={entry.timestamp}
    >
      {new Date(entry.timestamp).toLocaleString()}
    </time>
  </article>
);
