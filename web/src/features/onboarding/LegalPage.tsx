import { Link, Navigate, useParams } from 'react-router-dom';
import { LEGAL_LAST_UPDATED, PRIVACY_POLICY, TERMS_OF_SERVICE } from './legalContent';

const DOCUMENTS = {
  privacy: { title: 'Privacy Policy', sections: PRIVACY_POLICY },
  terms: { title: 'Terms of Service', sections: TERMS_OF_SERVICE },
} as const;

export function LegalPage() {
  const { document } = useParams<{ document: string }>();
  if (document !== 'privacy' && document !== 'terms') return <Navigate to="/login" replace />;
  const { title, sections } = DOCUMENTS[document];

  return (
    <main className="mx-auto max-w-2xl px-4 py-10">
      <Link to="/login" className="inline-flex h-12 items-center text-base font-semibold text-brand-strong underline">
        Back to sign in
      </Link>
      <h1 className="mt-2 text-3xl font-bold tracking-tight">{title}</h1>
      <p className="mt-1 text-base text-ink-soft">{LEGAL_LAST_UPDATED}</p>
      <div className="mt-8 flex flex-col gap-6">
        {sections.map((section) => (
          <section key={section.heading}>
            <h2 className="text-xl font-bold">{section.heading}</h2>
            <p className="mt-2 whitespace-pre-line text-base leading-relaxed">{section.body}</p>
          </section>
        ))}
      </div>
    </main>
  );
}
