namespace Back2Base.SchedulingDemo
{
    public class DataModel
    {
        public string[] Employees =
        {
            "Linda",
            "Sam",
            "Tim",
            "Tina",
            "Michelle",
            "Jackie",
            "George",
            "Nat"
        };

        public int NumEmployees => 8;
        public int NumWeeks => 3;
        public int NumDays => NumWeeks * 7;
        public int NumShifts => 4;
        
        /// <summary>
        /// Consecutive shift constraints:
        ///     (shift, hard_min, soft_min, min_penalty, soft_max, hard_max, max_penalty)
        /// </summary>
        public (int Shift, int HardMin, int SoftMin, int MinPenalty, int SoftMax, int HardMax, int MaxPenalty)[] ConsecutiveShiftConstraints =
        {
            // One or two consecutive days of rest, this is a hard constraint.
            (0, 1, 1, 0, 2, 2, 0),
            // Between 2 and 3 consecutive days of night shifts, 1 and 4 are
            // possible but penalized.
            (3, 1, 2, 20, 3, 4, 5),
        };

        /// <summary>
        /// Fixed assignments: (employee, shift, day).
        /// This fixes the first 2 days of the schedule.
        /// </summary>
        public (int Employee, int Shift, int Day)[] FixedAssignments =
        {
            (0, 0, 0),
            (1, 0, 0),
            (2, 1, 0),
            (3, 1, 0),
            (4, 2, 0),
            (5, 2, 0),
            (6, 2, 3),
            (7, 3, 0),
            (0, 1, 1),
            (1, 1, 1),
            (2, 2, 1),
            (3, 2, 1),
            (4, 2, 1),
            (5, 0, 1),
            (6, 0, 1),
            (7, 3, 1),
        };

        /// <summary>
        /// Employee Shift Requests
        /// Request: (employee, shift, day, weight)
        /// <remark>A negative weight indicates that the employee desires this assignment.</remark>
        /// </summary>
        public (int Employee, int Shift, int Day, int Weight)[] EmployeeRequests = 
        {
            // Employee 3 wants the first Saturday off.
            (3, 0, 5, -2),
            // Employee 5 wants a night shift on the second Thursday.
            (5, 3, 10, -2),
            // Employee 2 does not want a night shift on the third Friday.
            (2, 3, 4, 4)
        };

        /// <summary>
        /// The daily demands for work shifts (morning, afternoon, night) for each day  of the week starting on Monday.
        /// </summary>
        public int[][] DailyShiftDemands =
        {
            new [] {2, 3, 1}, // Monday
            new [] {2, 3, 1}, // Tuesday
            new [] {2, 2, 2}, // Wednesday
            new [] {2, 3, 1}, // Thursday
            new [] {2, 2, 2}, // Friday
            new [] {1, 2, 3}, // Saturday
            new [] {1, 3, 1}, // Sunday
        };

        /// <summary>
        /// Shift transition penalties, (previous_shift, next_shift, penalty (0 means forbidden))
        /// </summary>
        public (int PreviousShift, int NextShift, int Penalty)[] ShiftTransitionPenalties = 
        {
            // Afternoon to night has a penalty of 4.
            (2, 3, 4),
            // Night to morning is forbidden.
            (3, 1, 0),
        };

        /// <summary>
        /// Penalty for exceeding the cover constraint per shift type. ie. more employees than required covering a shift.
        /// </summary>
        public int[] ExcessCoverPenalties = { 2, 2, 5 };

        /// <summary>
        /// Weekly sum constraints on shifts days:
        ///     (shift, hardMin, softMin, minPenalty, softMax, hardMax, maxPenalty) 
        /// </summary>
        public (int Shift, int HardMin, int SoftMin, int MinPenalty, int SoftMax, int HardMax, int MaxPenalty)[] EmployeeShiftsPerWeekConstraints =
        {
            // Each employee must have an absolute minimum of 1 rest day per week, and an absolute maximum of 3, ideally having 2.
            (0, 1, 2, 7, 2, 3, 4),
            // Each employee should ideally have between 1 and 4 night shifts per week, with an absolute minimum of 0 and an absolute maximum of 4.
            (3, 0, 1, 3, 4, 4, 0),
        };
    }
}
