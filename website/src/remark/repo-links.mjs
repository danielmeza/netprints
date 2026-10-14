// Rewrites a Markdown link or image whose relative target resolves outside docs/ (for example
// ../../specs/003-core-refactor/plan.md from an ADR, or ../../src/NetPrints.Core/…) to an absolute
// GitHub URL, so the source files stay useful on GitHub (where docs/ is just another folder) while
// the built site never links outside itself (onBrokenLinks: 'throw' only checks in-site targets).
//
// An image that resolves under website/static (the guides' screenshots) becomes a site-root URL
// (/img/...), so the same relative path renders on GitHub and on the site.
//
// Links already absolute (http(s):, mailto:, #fragment) or resolving inside docs/ are left alone;
// the former are untouched, the latter are handled by Docusaurus's own link resolution.
import path from 'node:path';

const ABSOLUTE = /^([a-z]+:|#)/i;

// Minimal depth-first walk so the plugin needs no extra dependency beyond what Docusaurus already
// brings in (mdast nodes expose their children, if any, under `.children`).
function walk(node, visitor) {
  if (node.type === 'link' || node.type === 'image') visitor(node);
  for (const child of node.children ?? []) walk(child, visitor);
}

export default function repoLinks({repo, branch}) {
  return (tree, file) => {
    const docsRoot = path.resolve(file.cwd, '..', 'docs');
    const staticRoot = path.resolve(file.cwd, 'static');
    const fileDir = path.dirname(file.path);

    walk(tree, (node) => {
      const url = node.url;
      if (!url || ABSOLUTE.test(url)) return;

      const [target, hash = ''] = url.split('#');
      if (!target) return; // pure in-page fragment

      const resolved = path.resolve(fileDir, target);
      const relativeToStatic = path.relative(staticRoot, resolved);
      if (node.type === 'image' && !relativeToStatic.startsWith('..') && !path.isAbsolute(relativeToStatic)) {
        node.url = `/${relativeToStatic.split(path.sep).join('/')}`; // an image under website/static is served by the site itself
        return;
      }

      const relativeToDocsRoot = path.relative(docsRoot, resolved);
      const escapesDocs = relativeToDocsRoot.startsWith('..') || path.isAbsolute(relativeToDocsRoot);
      if (!escapesDocs) return;

      const repoRoot = path.resolve(docsRoot, '..');
      const repoRelativePath = path.relative(repoRoot, resolved).split(path.sep).join('/');
      const segment = node.type === 'image' ? 'raw' : 'blob';
      node.url = `${repo}/${segment}/${branch}/${repoRelativePath}${hash ? `#${hash}` : ''}`;
    });
  };
}
