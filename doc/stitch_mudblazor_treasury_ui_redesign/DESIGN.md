---
name: Treasury Financial System
colors:
  surface: '#f8f9ff'
  surface-dim: '#cbdbf5'
  surface-bright: '#f8f9ff'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#eff4ff'
  surface-container: '#e5eeff'
  surface-container-high: '#dce9ff'
  surface-container-highest: '#d3e4fe'
  on-surface: '#0b1c30'
  on-surface-variant: '#45464d'
  inverse-surface: '#213145'
  inverse-on-surface: '#eaf1ff'
  outline: '#76777d'
  outline-variant: '#c6c6cd'
  surface-tint: '#565e74'
  primary: '#000000'
  on-primary: '#ffffff'
  primary-container: '#131b2e'
  on-primary-container: '#7c839b'
  inverse-primary: '#bec6e0'
  secondary: '#006a61'
  on-secondary: '#ffffff'
  secondary-container: '#86f2e4'
  on-secondary-container: '#006f66'
  tertiary: '#000000'
  on-tertiary: '#ffffff'
  tertiary-container: '#2a1700'
  on-tertiary-container: '#b87500'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#dae2fd'
  primary-fixed-dim: '#bec6e0'
  on-primary-fixed: '#131b2e'
  on-primary-fixed-variant: '#3f465c'
  secondary-fixed: '#89f5e7'
  secondary-fixed-dim: '#6bd8cb'
  on-secondary-fixed: '#00201d'
  on-secondary-fixed-variant: '#005049'
  tertiary-fixed: '#ffddb8'
  tertiary-fixed-dim: '#ffb95f'
  on-tertiary-fixed: '#2a1700'
  on-tertiary-fixed-variant: '#653e00'
  background: '#f8f9ff'
  on-background: '#0b1c30'
  surface-variant: '#d3e4fe'
typography:
  display-lg:
    fontFamily: Inter
    fontSize: 40px
    fontWeight: '700'
    lineHeight: 48px
    letterSpacing: -0.02em
  display-lg-mobile:
    fontFamily: Inter
    fontSize: 30px
    fontWeight: '700'
    lineHeight: 38px
    letterSpacing: -0.02em
  headline-lg:
    fontFamily: Inter
    fontSize: 28px
    fontWeight: '600'
    lineHeight: 36px
    letterSpacing: -0.015em
  headline-md:
    fontFamily: Inter
    fontSize: 22px
    fontWeight: '600'
    lineHeight: 28px
    letterSpacing: -0.01em
  headline-sm:
    fontFamily: Inter
    fontSize: 18px
    fontWeight: '600'
    lineHeight: 24px
  title-md:
    fontFamily: Inter
    fontSize: 15px
    fontWeight: '600'
    lineHeight: 20px
  body-lg:
    fontFamily: Inter
    fontSize: 16px
    fontWeight: '400'
    lineHeight: 24px
  body-md:
    fontFamily: Inter
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
  body-sm:
    fontFamily: Inter
    fontSize: 12px
    fontWeight: '400'
    lineHeight: 16px
  data-mono-lg:
    fontFamily: JetBrains Mono
    fontSize: 22px
    fontWeight: '500'
    lineHeight: 28px
    letterSpacing: -0.02em
  data-mono-md:
    fontFamily: JetBrains Mono
    fontSize: 14px
    fontWeight: '500'
    lineHeight: 20px
    letterSpacing: -0.01em
  data-mono-sm:
    fontFamily: JetBrains Mono
    fontSize: 12px
    fontWeight: '400'
    lineHeight: 16px
  label-md:
    fontFamily: Inter
    fontSize: 12px
    fontWeight: '600'
    lineHeight: 16px
    letterSpacing: 0.04em
  label-xs:
    fontFamily: Inter
    fontSize: 11px
    fontWeight: '500'
    lineHeight: 14px
    letterSpacing: 0.02em
rounded:
  sm: 0.125rem
  DEFAULT: 0.25rem
  md: 0.375rem
  lg: 0.5rem
  xl: 0.75rem
  full: 9999px
spacing:
  space-2xs: 0.25rem
  space-xs: 0.5rem
  space-sm: 0.75rem
  space-md: 1rem
  space-lg: 1.5rem
  space-xl: 2rem
  space-2xl: 3rem
  gutter-mobile: 1rem
  gutter-desktop: 1.5rem
  margin-mobile: 1rem
  margin-tablet: 1.5rem
  margin-desktop: 2rem
---

## Brand & Style

This design system embodies the calm precision, authority, and lucidity of an institutional wealth platform scaled down to an intuitive personal interface. The emotional experience balances absolute security with optimism: the interface never induces anxiety over market volatility, but instead conveys clarity, control, and actionable intelligence.

Drawing from modern enterprise Material and MudBlazor design aesthetics, the interface adopts a refined, utility-forward modernism. Rather than relying on heavy gradients or decorative flourishes, depth and hierarchy are articulated through structured grids, crisp borders, subtle layered surface tiers, and decisive functional color cues.

## Colors

The palette establishes an immediate institutional tone with functional, meaning-infused accents:

- **Primary (`#0F172A`)**: Deep Slate Navy represents base authority and structural grounding. Used for top-level navigation, primary buttons, critical data metrics, and high-emphasis typography.
- **Secondary (`#0D9488`)**: Deep Teal/Emerald conveys liquidity, portfolio appreciation, asset health, and primary confirmations. Paired with `#10B981` for positive delta indicators.
- **Tertiary (`#F59E0B`)**: Warm Amber/Gold functions as an intentional highlight for pending transactions, proactive budget thresholds, yield opportunities, and attention-required status items.
- **Neutral (`#64748B`)**: Slate neutral provides balanced mid-tones for secondary labels, table headers, inactive states, and structural dividers.
- **Backgrounds**: Base canvas defaults to ultra-clean Slate 50 (`#F8FAFC`), while cards and elevated modules sit on stark Pure White (`#FFFFFF`) with precise 1px borders (`#E2E8F0`).

## Typography

Typography pairs `Inter` for interface structure, prose, and navigation with `JetBrains Mono` for ledger transactions, currency figures, and asset tickers. 

- **Numeric Tabular Figures**: When rendering financial tables or asset totals using `Inter`, enforce tabular lining figures (`font-variant-numeric: tabular-nums`) so numbers align vertically across rows and column groups.
- **Monospaced Data**: Use `JetBrains Mono` for account identifiers, IBAN/Routing numbers, stock tickers, and granular timestamp entries.
- **Micro-Labels**: Metric subheaders, uppercase column headers, and status pill copy use `label-md` or `label-xs` with expanded tracking to maintain legibility at glance speed.

## Layout & Spacing

The layout is built on a responsive 12-column grid system with an 8px base rhythm:

- **Desktop (1280px+)**: 12-column layout with 24px gutters and 32px external margins. Maximum content width caps at 1440px for comfortable high-density reading.
- **Tablet (768px - 1279px)**: 8-column layout with 20px gutters and 24px margins. Sub-dashboards collapse into 2-up stacks.
- **Mobile (< 768px)**: 4-column fluid layout with 16px gutters and 16px margins. Quick actions and KPI summary metric cards scroll horizontally or stack vertically.
- **Information Density**: Financial dashboards prioritize condensed vertical flow. Data tables default to a compact 44px row height, expandable to 56px when secondary metadata (e.g., category or merchant icon) is displayed.

## Elevation & Depth

Visual depth is achieved through low-contrast hairline outlines coupled with ultra-soft ambient drop shadows, reflecting the precision of modern Material/MudBlazor design:

- **Flat / Ground Level (Canvas)**: Surface at `#F8FAFC`, zero elevation.
- **Level 1 (Default Card / Tile)**: Solid white (`#FFFFFF`) surface framed by a 1px solid border (`#E2E8F0`) and an ambient shadow: `box-shadow: 0 1px 3px 0 rgba(15, 23, 42, 0.05), 0 1px 2px -1px rgba(15, 23, 42, 0.03)`.
- **Level 2 (Hovered Cards / Dropdowns / Popovers)**: Solid white surface with `box-shadow: 0 4px 6px -1px rgba(15, 23, 42, 0.07), 0 2px 4px -2px rgba(15, 23, 42, 0.05)`.
- **Level 3 (Modals / Drawers / Quick-Transfer Sheets)**: Floated surface with `box-shadow: 0 20px 25px -5px rgba(15, 23, 42, 0.1), 0 8px 10px -6px rgba(15, 23, 42, 0.05)`.
- **Accent Tonal Underlays**: Key summary KPI modules (e.g., total liquid net worth) employ a faint tint layer (such as Teal at 4% opacity: `rgba(13, 148, 136, 0.04)`) to anchor focus without introducing heavy dark mode containers.

## Shapes

The interface employs a disciplined, soft geometry (`roundedness: 1`):

- Standard interactive controls, form fields, and table headers feature a subtle **4px** (`0.25rem`) border radius.
- Cards, data visualization containers, and modal dialogs adopt an **8px** (`0.5rem`) corner radius.
- Compact informational pills, quick action chips, and status badges utilize a **9999px** full-pill radius to contrast sharply against structured rectangular data tables and cards.

## Components

### Buttons
- **Primary**: Solid Slate Navy (`#0F172A`) with high-contrast pure white text, 8px border radius, 40px default height. Hover shifts to `#1E293B`. Active state applies a subtle 0.98 scale transform.
- **Secondary / Outlined**: 1px solid `#CBD5E1` border, transparent background, text `#0F172A`. Hover transitions to `#F1F5F9`.
- **Tertiary Accent (Action)**: Solid Teal (`#0D9488`) with white text for money movements, transfers, and completed deposits.

### Input Fields & Selects
- Height: 40px standard, 32px dense.
- Background: `#FFFFFF`. Border: 1px solid `#CBD5E1`.
- Focus State: Border color transitions to `#0D9488` accompanied by a 2px outer glow (`box-shadow: 0 0 0 3px rgba(13, 148, 136, 0.15)`).
- Leading / Trailing Add-ons: Currency symbols (e.g., "$", "€") render in neutral `#64748B` with a monospaced font weight of 500.

### Cards & Financial Tiles
- Encapsulate distinct financial contexts (e.g., Accounts, Monthly Cash Flow, Asset Allocation).
- Built with a solid white background, 1px border (`#E2E8F0`), 8px border radius, and Level 1 elevation.
- Card headers strictly separate action triggers from metadata via a clear 16px bottom margin or a muted bottom divider line.

### Quick Action Chips
- Elliptical (full-pill) shape with 28px height.
- Inactive: Background `#F1F5F9`, text `#334155`, borderless.
- Active/Hover: Background `#E2E8F0` or tinted `#CCFBF1` with `#0F766E` text when toggling positive cash flow filters.

### Financial Data Tables
- Header row: Text in `label-xs` uppercase, neutral `#64748B`, background `#F8FAFC`, 36px height.
- Rows: 48px height, separated by a 1px border (`#F1F5F9`). Alternating hover state of `#F8FAFC`.
- Positive balances / cash inflows: `#0D9488` with a leading `+`.
- Negative balances / outflows: `#E11D48` with a leading `−`.

### Progress Bars & Budget Meters
- Track: 6px height with a full-pill radius, colored `#E2E8F0`.
- Indicator: Vibrant Teal (`#0D9488`) by default; transitions dynamically to Amber (`#F59E0B`) at 85% budget utilization, and Carmine (`#E11D48`) when exceeded.

### Status Badges
- Pill format with 20px height, 8px horizontal padding, and `label-xs` typography.
- **Settled / Positive**: `#ECFDF5` background with `#047857` text and an optional `#10B981` leading micro-dot.
- **Pending / Action**: `#FFFBEB` background with `#B45309` text.
- **Draft / Inactive**: `#F1F5F9` background with `#475569` text.