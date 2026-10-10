import type {Config} from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';
import repoLinks from './src/remark/repo-links.mjs';

const config: Config = {
  title: 'NetPrints',
  tagline: 'Visual programming for .NET that compiles to readable C#',
  favicon: 'img/favicon.png',
  url: 'https://danielmeza.github.io',
  baseUrl: '/netprints/',
  organizationName: 'danielmeza',
  projectName: 'netprints',
  trailingSlash: true,
  onBrokenLinks: 'throw',
  onBrokenAnchors: 'warn',
  markdown: {
    format: 'detect',                       // .md = CommonMark, only .mdx = MDX
    hooks: {onBrokenMarkdownLinks: 'throw', onBrokenMarkdownImages: 'throw'},
  },
  presets: [
    ['classic', {
      docs: {
        path: '../docs',
        routeBasePath: '/',
        include: ['**/*.md'],
        exclude: ['api/**', '**/prototypes/**'],
        numberPrefixParser: false,             // routes keep the file names (adr/0005-…, research/2026-09-25-…)
        editUrl: 'https://github.com/danielmeza/netprints/edit/master/docs/',
        beforeDefaultRemarkPlugins: [[repoLinks, {repo: 'https://github.com/danielmeza/netprints', branch: 'master'}]],
        // no sidebarPath: the sidebar is generated from folders, _category_.json and front matter
      },
      blog: false,
      theme: {customCss: './src/css/custom.css'},
    } satisfies Preset.Options],
  ],
  themeConfig: {
    navbar: {
      title: 'NetPrints',
      logo: {alt: 'NetPrints', src: 'img/logo.svg'},
      items: [
        {type: 'docSidebar', sidebarId: 'defaultSidebar', label: 'Docs', position: 'left'},
        {href: 'https://danielmeza.github.io/netprints/api/', label: 'API', position: 'left', target: '_self'},
        {href: 'https://github.com/danielmeza/netprints/releases', label: 'Download', position: 'right'},
        {href: 'https://github.com/danielmeza/netprints', label: 'GitHub', position: 'right'},
      ],
    },
    footer: {
      style: 'dark',
      copyright: 'MIT licence. NetPrints continues the original NetPrints by Robin Kahlow (github.com/RobinKa/netprints).',
    },
  } satisfies Preset.ThemeConfig,
};

export default config;
