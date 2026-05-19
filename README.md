# ApexBreakout

NinjaTrader 8 Strategy - **ORB 3-Candle Rejection / Bounce Strategy** with fixed 1:2 Risk-Reward.

## Strategy Overview

This strategy identifies the **Opening Range Breakout (ORB)** during a defined session window and then looks for high-probability **rejection / bounce** setups at the ORB boundaries.

Instead of chasing breakouts, it waits for price to break out and then **reject** back into the range before entering in the direction of the rejection.

## How the ORB is Built

- The strategy builds the ORB (Opening Range High and Low) between `Session Start Time` and `Session End Time` (default: 1:00 AM – 3:00 AM).
- Once the ORB is formed, a gray rectangle is drawn and **extended all the way to the next trading session** for visual reference.
- Key levels drawn:
  - Midpoint (50%)
  - 25% and 75% levels

## Entry Rules (3-Candle Confirmation)

### Long Entries (Bullish Rejection)

**1. Rejection at Top of ORB:**
- Candle 2 and Candle 1 both close **above** ORB High
- Current candle (Candle 0) pulls back and touches ORB High from above
- Enter **Long** at the ORB High level

**2. Rejection at Bottom of ORB:**
- Candle 2 touches or breaks below ORB Low
- Candle 1 closes back **inside** the ORB
- Current candle touches or breaks below ORB Low again
- Enter **Long** at the ORB Low level

### Short Entries (Bearish Rejection)

**1. Rejection at Top of ORB:**
- Candle 2 touches or breaks above ORB High
- Candle 1 closes back **inside** the ORB
- Current candle touches or breaks above ORB High again
- Enter **Short** at the ORB High level

**2. Rejection at Bottom of ORB:**
- Candle 2 and Candle 1 both close **below** ORB Low
- Current candle pulls back and touches ORB Low from below
- Enter **Short** at the ORB Low level

## Exit Rules (1:2 Risk-Reward)

- **Profit Target**: ORB Midpoint (50% level of the range)
- **Stop Loss**: 25% beyond the opposite side of the ORB
  - Long stop = ORB Low - 0.25 × Range
  - Short stop = ORB High + 0.25 × Range

Additionally, an optional **EMA Trailing Exit** can be enabled.

## Risk Management

- Daily loss limit
- Max daily trades
- Profit activation + trailing drawdown
- News blockout period (default 8:25–8:35 AM)
- Optional Institutional Baseline EMA filter

## Key Parameters

- **Session Start / End Time**: Defines ORB formation window
- **Breakout Confirmation Offset**: Not currently used in rejection mode
- **Use Institutional Baseline**: Filters entries based on EMA
- **Use EMA Trailing Exit**: Dynamic exit option

## Important Notes

- Designed primarily for **1-minute or tick charts**
- Works best on volatile instruments (e.g. /MES, /MNQ, /MGC)
- The strategy only takes **one trade per direction per day**

---

**Strategy Name**: ApexBreakout
**Platform**: NinjaTrader 8
**Author**: jbkatutu + Grok
