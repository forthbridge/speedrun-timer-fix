using System;

namespace SpeedrunTimerFix;

// One of these is attached to each campaign
public sealed class CampaignTimerSaveData
{
    // Free: Timer is incremented in TimeTick

    // Time is initially counted to the undetermined timer
    // It is converted to the following under the mentioned circumstances:

    // Completed: cycle survived and player was not starving
    // Lost: player died during the cycle, or on game start (this means the player exited with an ongoing starve cycle)
    public double UndeterminedFreeTime { get; set; }
    public double CompletedFreeTime { get; set; }
    public double LostFreeTime { get; set; }
    
    public double TotalFreeTime => CompletedFreeTime + LostFreeTime + UndeterminedFreeTime;
    public TimeSpan TotalFreeTimeSpan => TimeSpan.FromMilliseconds(TotalFreeTime);


    // Fixed: Timer is incremented within the fixed update loop    
    public double UndeterminedFixedTime { get; set; }
    public double CompletedFixedTime { get; set; }
    public double LostFixedTime { get; set; }

    public double TotalFixedTime => CompletedFixedTime + LostFixedTime + UndeterminedFixedTime;
    public TimeSpan TotalFixedTimeSpan => TimeSpan.FromMilliseconds(TotalFixedTime);


    // Wipes all times associated with this tracker 
    public void WipeTimes()
    {
        UndeterminedFreeTime = 0.0;
        CompletedFreeTime = 0.0;
        LostFreeTime = 0.0;

        UndeterminedFixedTime = 0.0;
        CompletedFixedTime = 0.0;
        LostFixedTime = 0.0;
    }

    public void ConvertUndeterminedToLostTime()
    {
        LostFreeTime += UndeterminedFreeTime;
        UndeterminedFreeTime = 0.0;

        LostFixedTime += UndeterminedFixedTime;
        UndeterminedFixedTime = 0.0;
    }

    public void ConvertUndeterminedToCompletedTime()
    {
        CompletedFreeTime += UndeterminedFreeTime;
        UndeterminedFreeTime = 0.0;

        CompletedFixedTime += UndeterminedFixedTime;
        UndeterminedFixedTime = 0.0;
    }

    // Loads the old timing system's alive and dead times into the tracker
    public void LoadOldTimings(int gameTimeAlive, int gameTimeDead)
    {
        var timeAliveMilliseconds = gameTimeAlive * 1000.0;
        var timeDeadMilliseconds = gameTimeDead * 1000.0;

        CompletedFreeTime = timeAliveMilliseconds;
        LostFreeTime = timeDeadMilliseconds;

        CompletedFixedTime = timeAliveMilliseconds;
        LostFixedTime = timeDeadMilliseconds;
    }
}