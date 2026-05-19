#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion
namespace NinjaTrader.NinjaScript.Strategies
{
    public class ApexBreakout : Strategy
    {
        #region Variables & State
        private EMA institutionalBaseline;
        private EMA exitEma;
        private SMA volumeSma;
       
        private double dailyRealizedPnL = 0;
        private double highestDailyPnL = 0;
        private int dailyTradeCount = 0;
        private bool isHaltedForDay = false;
        private int currentDay = -1;
       
        private double orbHigh = double.MinValue;
        private double orbLow = double.MaxValue;
        private double orbRange = 0;
        private bool orbFormed = false;
        private bool hasTradedLongToday = false;
        private bool hasTradedShortToday = false;
        private bool limitsCheckedThisTick = false;
        #endregion
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "ApexBreakout - ORB 3-Candle Rejection/Bounce Strategy (1:2 RR)";
                Name = "ApexBreakout";
                Calculate = Calculate.OnPriceChange;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = true;
                ExitOnSessionCloseSeconds = 30;
                BarsRequiredToTrade = 50;
                DailyLossLimit = 500;
                ProfitActivation = 1500;
                TrailingDrawdown = 500;
                MaxDailyTrades = 5;
                SessionStart = 10000;
                SessionEnd = 30000;
                GlobalEnd = 150000;
               
                VolumeSmaPeriod = 20;
                HighVolumeMultiplier = 1.5;
                LowVolumeMultiplier = 0.6;
                UseInstitutionalBaseline = true;
                BaselinePeriod = 50;
                EnableNewsBlockout = true;
                NewsTimeStart = 82500;
                NewsTimeEnd = 83500;
               
                UseEmaExit = true;
                ExitEmaPeriod = 9;
            }
            else if (State == State.DataLoaded)
            {
                if (UseInstitutionalBaseline)
                {
                    institutionalBaseline = EMA(BaselinePeriod);
                    institutionalBaseline.Plots[0].Brush = Brushes.DarkOrange;
                    AddChartIndicator(institutionalBaseline);
                }
               
                if (UseEmaExit)
                {
                    exitEma = EMA(ExitEmaPeriod);
                    exitEma.Plots[0].Brush = Brushes.Cyan;
                    AddChartIndicator(exitEma);
                }
                volumeSma = SMA(Volume, VolumeSmaPeriod);
            }
        }
        protected override void OnBarUpdate()
        {
            if (CurrentBar < BarsRequiredToTrade) return;
            if (Time[0].Day != currentDay)
            {
                currentDay = Time[0].Day;
                dailyRealizedPnL = 0;
                highestDailyPnL = 0;
                dailyTradeCount = 0;
                isHaltedForDay = false;
                orbFormed = false;
                orbHigh = double.MinValue;
                orbLow = double.MaxValue;
                orbRange = 0;
                hasTradedLongToday = false;
                hasTradedShortToday = false;
            }
            if (isHaltedForDay) return;
            if (!limitsCheckedThisTick) { CheckDailyLimits(); limitsCheckedThisTick = true; }
            if (isHaltedForDay) return;
            MonitorExitsAndTraps();
            int currentTime = ToTime(Time[0]);
            if (EnableNewsBlockout && currentTime >= NewsTimeStart && currentTime <= NewsTimeEnd)
            {
                if (Position.MarketPosition != MarketPosition.Flat) Exit();
                return;
            }
            if (currentTime >= SessionStart && currentTime <= SessionEnd)
            {
                if (High[0] > orbHigh) orbHigh = High[0];
                if (Low[0] < orbLow) orbLow = Low[0];
                if (currentTime >= SessionEnd && !orbFormed)
                {
                    orbFormed = true;
                    orbRange = orbHigh - orbLow;
                    DateTime startTime = GetSessionStartTime(SessionStart);
                    DateTime endTime = Time[0].AddDays(1);  // extended to next session
                   
                    Draw.Rectangle(this, "ORB_Box_" + currentDay, startTime, orbHigh, endTime, orbLow, Brushes.Transparent).AreaBrush = Brushes.SlateGray;
                   
                    double midpoint = orbLow + (orbRange * 0.5);
                    double q25 = orbLow + (orbRange * 0.25);
                    double q75 = orbLow + (orbRange * 0.75);
                   
                    Draw.Line(this, "Mid_" + currentDay, false, startTime, midpoint, endTime, midpoint, Brushes.SlateGray, DashStyleHelper.Dash, 1);
                    Draw.Line(this, "Q25_" + currentDay, false, startTime, q25, endTime, q25, Brushes.SlateGray, DashStyleHelper.Dot, 1);
                    Draw.Line(this, "Q75_" + currentDay, false, startTime, q75, endTime, q75, Brushes.SlateGray, DashStyleHelper.Dot, 1);
                    Draw.Line(this, "Hunting_Start_" + currentDay, false, Time[0], orbLow, Time[0], orbHigh, Brushes.Gold, DashStyleHelper.Solid, 2);
                }
            }
            if (IsFirstTickOfBar) limitsCheckedThisTick = false;
            if (currentTime > GlobalEnd || !orbFormed || dailyTradeCount >= MaxDailyTrades || currentTime < SessionEnd) return;
            if (Position.MarketPosition != MarketPosition.Flat) return;
            bool baselineLongValid = !UseInstitutionalBaseline || Close[0] > institutionalBaseline[0];
            bool baselineShortValid = !UseInstitutionalBaseline || Close[0] < institutionalBaseline[0];
            // 3. Long entry at the top of the orb
            if (High[2] >= orbHigh && Close[2] > orbHigh && High[1] >= orbHigh && Close[1] > orbHigh && Low[0] <= orbHigh && baselineLongValid && !hasTradedLongToday)
            {
                hasTradedLongToday = true;
                ExecuteRejectionTrade(OrderAction.Buy);
            }
            // 4. Long entry at the bottom of the orb
            else if (Low[2] <= orbLow && Close[1] > orbLow && Low[0] <= orbLow && Close[0] > orbLow && baselineLongValid && !hasTradedLongToday)
            {
                hasTradedLongToday = true;
                ExecuteRejectionTrade(OrderAction.Buy);
            }
            // 5. Short entry at the top of the orb box
            else if (High[2] >= orbHigh && Close[1] < orbHigh && High[0] >= orbHigh && Close[0] < orbHigh && baselineShortValid && !hasTradedShortToday)
            {
                hasTradedShortToday = true;
                ExecuteRejectionTrade(OrderAction.SellShort);
            }
            // 6. Short entry at the bottom of the orb box
            else if (Low[2] <= orbLow && Close[1] < orbLow && Low[0] <= orbLow && Close[0] < orbLow && baselineShortValid && !hasTradedShortToday)
            {
                hasTradedShortToday = true;
                ExecuteRejectionTrade(OrderAction.SellShort);
            }
        }
        private void ExecuteRejectionTrade(OrderAction action)
        {
            if (State != State.Realtime) return;
            double midpoint = orbLow + (orbRange * 0.5);
            dailyTradeCount++;
            Brush markerColor = action == OrderAction.Buy ? Brushes.Cyan : Brushes.Magenta;
            if (action == OrderAction.Buy)
            {
                Draw.TriangleUp(this, "EntryL_" + CurrentBar, true, 0, Low[0] - (8 * TickSize), markerColor);
                EnterLong("BounceLong");
                SetProfitTarget("BounceLong", CalculationMode.Price, midpoint);
                SetStopLoss("BounceLong", CalculationMode.Price, orbLow - (orbRange * 0.25), false);
            }
            else
            {
                Draw.TriangleDown(this, "EntryS_" + CurrentBar, true, 0, High[0] + (8 * TickSize), markerColor);
                EnterShort("BounceShort");
                SetProfitTarget("BounceShort", CalculationMode.Price, midpoint);
                SetStopLoss("BounceShort", CalculationMode.Price, orbHigh + (orbRange * 0.25), false);
            }
        }
        private void MonitorExitsAndTraps()
        {
            if (UseEmaExit && Position.MarketPosition != MarketPosition.Flat)
            {
                if (Position.MarketPosition == MarketPosition.Long && Close[0] < exitEma[0]) ExitLong();
                else if (Position.MarketPosition == MarketPosition.Short && Close[0] > exitEma[0]) ExitShort();
            }
        }
        private void CheckDailyLimits()
        {
            double totalDailyPnL = dailyRealizedPnL + (Position.MarketPosition != MarketPosition.Flat ? Position.GetUnrealizedProfitLoss(PerformanceUnit.Currency) : 0);
            if (totalDailyPnL > highestDailyPnL) highestDailyPnL = totalDailyPnL;
            double currentStopLimit = -DailyLossLimit;
            if (highestDailyPnL >= ProfitActivation) currentStopLimit = highestDailyPnL - TrailingDrawdown;
            if (totalDailyPnL <= currentStopLimit || dailyTradeCount >= MaxDailyTrades) { isHaltedForDay = true; Exit(); }
        }
        private DateTime GetSessionStartTime(int t)
        {
            for (int i = 0; i < CurrentBar; i++)
                if (ToTime(Time[i]) <= t) return Time[i];
            return Time[0];
        }
        private void Exit()
        {
            if (Position.MarketPosition == MarketPosition.Long) ExitLong();
            else if (Position.MarketPosition == MarketPosition.Short) ExitShort();
        }
        #region Properties
        [NinjaScriptProperty, Display(Name="Daily Loss Limit ($)", Order=1, GroupName="1. Risk")]
        public double DailyLossLimit { get; set; }
       
        [NinjaScriptProperty, Display(Name="Max Daily Trades", Order=2, GroupName="1. Risk")]
        public int MaxDailyTrades { get; set; }
        [NinjaScriptProperty, Display(Name="Activate Profit Trail At ($)", Order=3, GroupName="1. Risk")]
        public double ProfitActivation { get; set; }
        [NinjaScriptProperty, Display(Name="Trailing Drawdown Amount ($)", Order=4, GroupName="1. Risk")]
        public double TrailingDrawdown { get; set; }
        [NinjaScriptProperty, Display(Name="Volume SMA Period", Order=4, GroupName="2. Execution")]
        public int VolumeSmaPeriod { get; set; }
        [NinjaScriptProperty, Display(Name="High Volume Multiplier (x)", Order=5, GroupName="2. Execution")]
        public double HighVolumeMultiplier { get; set; }
        [NinjaScriptProperty, Display(Name="Low Volume Multiplier (x)", Order=6, GroupName="2. Execution")]
        public double LowVolumeMultiplier { get; set; }
        [NinjaScriptProperty, Display(Name="Session Start Time", Order=1, GroupName="4. Timing")]
        public int SessionStart { get; set; }
        [NinjaScriptProperty, Display(Name="Session End Time", Order=2, GroupName="4. Timing")]
        public int SessionEnd { get; set; }
        [NinjaScriptProperty, Display(Name="Global End Time", Order=3, GroupName="4. Timing")]
        public int GlobalEnd { get; set; }
        [NinjaScriptProperty, Display(Name="Use Institutional Baseline (EMA)", Order=1, GroupName="5. Filters & News")]
        public bool UseInstitutionalBaseline { get; set; }
       
        [NinjaScriptProperty, Display(Name="Baseline Period", Order=2, GroupName="5. Filters & News")]
        public int BaselinePeriod { get; set; }
        [NinjaScriptProperty, Display(Name="Enable News Blockout", Order=3, GroupName="5. Filters & News")]
        public bool EnableNewsBlockout { get; set; }
        [NinjaScriptProperty, Display(Name="News Blockout Start", Order=4, GroupName="5. Filters & News")]
        public int NewsTimeStart { get; set; }
        [NinjaScriptProperty, Display(Name="News Blockout End", Order=5, GroupName="5. Filters & News")]
        public int NewsTimeEnd { get; set; }
        [NinjaScriptProperty, Display(Name="Use EMA Trailing Exit", Order=1, GroupName="6. Dynamic Exits")]
        public bool UseEmaExit { get; set; }
       
        [NinjaScriptProperty, Display(Name="Exit EMA Period", Order=2, GroupName="6. Dynamic Exits")]
        public int ExitEmaPeriod { get; set; }
        #endregion
    }
}