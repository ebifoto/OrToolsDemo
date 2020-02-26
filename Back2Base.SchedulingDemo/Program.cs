using System;
using System.Collections.Generic;
using System.Linq;
using Google.OrTools.Sat;
using IntVar = Google.OrTools.Sat.IntVar;

namespace Back2Base.SchedulingDemo
{
    /// <summary>
    /// Creates a shift scheduling problem and solves it
    /// </summary>
    public static class Program
    {
        static void Main(string[] args)
        {
            var data = new DataModel();
            SolveShiftScheduling(data);
        }

        static void SolveShiftScheduling(DataModel data)
        {
            var model = new CpModel();

            var vars = new SolverVars(new IntVar[data.NumEmployees, data.NumShifts, data.NumDays]);

            // Add bool var for each employee, shift and day to indicate whether the employee is working that shift or not
            foreach (var employee in Range(data.NumEmployees))
            {
                foreach (var shift in Range(data.NumShifts))
                {
                    foreach (var day in Range(data.NumDays))
                    {
                        vars.Work[employee, shift, day] = model.NewBoolVar($"work{employee}_{shift}_{day}");
                    }
                }
            }

            AddEmployeeDailyShiftLimits(model, vars, data);
            AddFixedAssignments(model, vars, data);
            AddEmployeeRequests(model, vars, data);
            AddShiftConstraints(model, vars, data);
            AddWeeklySumConstraints(model, vars, data);
            AddShiftTransitionPenalties(model, vars, data);
            AddCoverConstraints(model, vars, data);

            // Objective
            var objBoolSum = LinearExpr.ScalProd(vars.ObjBoolVars, vars.ObjBoolCoeffs);
            var objIntSum = LinearExpr.ScalProd(vars.ObjIntVars, vars.ObjIntCoeffs);

            model.Minimize(objBoolSum + objIntSum);

            // Solve model
            var solver = new CpSolver();
            solver.StringParameters = "num_search_workers:8, log_search_progress: true, max_time_in_seconds:30";

            var status = solver.Solve(model);

            ThrowIfUnsolved(status);

            vars.Status = status;
            PrintSolution(solver, vars, data);
        }

        private static void AddWeeklySumConstraints(CpModel model, SolverVars vars, DataModel data)
        {
            // Weekly sum constraints
            foreach (var constraint in data.EmployeeShiftsPerWeekConstraints)
            {
                foreach (var employee in Range(data.NumEmployees))
                {
                    foreach (var week in Range(data.NumWeeks))
                    {
                        var works = new IntVar[7];

                        foreach (var day in Range(7))
                        {
                            works[day] = vars.Work[employee, constraint.Shift, day + week * 7];
                        }

                        var (variables, coeffs) = AddSoftSumConstraint(
                            model, works,
                            constraint.HardMin, constraint.SoftMin, constraint.MinPenalty,
                            constraint.SoftMax, constraint.HardMax, constraint.MaxPenalty,
                            $"weekly_sum_constraint(employee {employee}, shift {constraint.Shift}, week {week}");

                        vars.ObjBoolVars.AddRange(variables);
                        vars.ObjBoolCoeffs.AddRange(coeffs);
                    }
                }
            }
        }

        private static void AddCoverConstraints(CpModel model, SolverVars solverVars, DataModel data)
        {
            // Cover constraints
            foreach (var shift in Range(1, data.NumShifts))
            {
                foreach (var week in Range(data.NumWeeks))
                {
                    foreach (var day in Range(7))
                    {
                        var works = new IntVar[data.NumEmployees];
                        foreach (var employee in Range(data.NumEmployees))
                        {
                            works[employee] = solverVars.Work[employee, shift, week * 7 + day];
                        }

                        // Ignore off shift
                        var minDemand = data.DailyShiftDemands[day][shift - 1];
                        var worked = model.NewIntVar(minDemand, data.NumEmployees, "");
                        model.Add(LinearExpr.Sum(works) == worked);

                        var overPenalty = data.ExcessCoverPenalties[shift - 1];
                        if (overPenalty > 0)
                        {
                            var name = $"excess_demand(shift={shift}, week={week}, day={day}";
                            var excess = model.NewIntVar(0, data.NumEmployees - minDemand, name);
                            model.Add(excess == worked - minDemand);
                            solverVars.ObjIntVars.Add(excess);
                            solverVars.ObjIntCoeffs.Add(overPenalty);
                        }
                    }
                }
            }
        }

        private static void AddShiftConstraints(CpModel model, SolverVars solverVars, DataModel data)
        {
            // Shift constraints
            foreach (var constraint in data.ConsecutiveShiftConstraints)
            {
                foreach (var employee in Range(data.NumEmployees))
                {
                    var works = new IntVar[data.NumDays];
                    foreach (var day in Range(data.NumDays))
                    {
                        works[day] = solverVars.Work[employee, constraint.Shift, day];
                    }

                    var (variables, coeffs) = AddSoftSequenceConstraint(
                        model, works,
                        constraint.HardMin, constraint.SoftMin, constraint.MinPenalty,
                        constraint.SoftMax, constraint.HardMax, constraint.MaxPenalty,
                        $"shift_constraint(employee {employee}, shift {constraint.Shift}");

                    solverVars.ObjBoolVars.AddRange(variables);
                    solverVars.ObjBoolCoeffs.AddRange(coeffs);
                }
            }

        }

        private static void AddEmployeeRequests(CpModel model, SolverVars vars, DataModel data)
        {
            foreach (var (employee, shift, day, weight) in data.EmployeeRequests)
            {
                vars.ObjBoolVars.Add(vars.Work[employee, shift, day]);
                vars.ObjBoolCoeffs.Add(weight);
            }
        }

        private static void AddEmployeeDailyShiftLimits(CpModel model, SolverVars vars, DataModel data)
        {
            // Exactly one shift per day.
            foreach (var employee in Range(data.NumEmployees))
            {
                foreach (var day in Range(data.NumDays))
                {
                    var temp = new IntVar[data.NumShifts];
                    foreach (var shift in Range(data.NumShifts))
                    {
                        temp[shift] = vars.Work[employee, shift, day];
                    }

                    model.Add(LinearExpr.Sum(temp) == 1);
                }
            }
        }

        private static void AddFixedAssignments(CpModel model, SolverVars vars, DataModel data)
        {
            foreach (var (employee, shift, day) in data.FixedAssignments)
            {
                model.Add(vars.Work[employee, shift, day] == 1);
            }
        }

        private static void AddShiftTransitionPenalties(CpModel model, SolverVars vars, DataModel data)
        {
            // Penalized transitions
            foreach (var penalizedTransition in data.ShiftTransitionPenalties)
            {
                foreach (var employee in Range(data.NumEmployees))
                {
                    foreach (var day in Range(data.NumDays - 1))
                    {
                        var transition = new List<ILiteral>()
                        {
                            vars.Work[employee, penalizedTransition.PreviousShift, day].Not(),
                            vars.Work[employee, penalizedTransition.NextShift, day + 1].Not()
                        };

                        if (penalizedTransition.Penalty == 0)
                        {
                            model.AddBoolOr(transition);
                        }
                        else
                        {
                            var transVar = model.NewBoolVar($"transition (employee {employee}, day={day}");
                            transition.Add(transVar);
                            model.AddBoolOr(transition);
                            vars.ObjBoolVars.Add(transVar);
                            vars.ObjBoolCoeffs.Add(penalizedTransition.Penalty);
                        }
                    }
                }
            }
        }

        private static void ThrowIfUnsolved(CpSolverStatus status)
        {
            switch (status)
            {
                case CpSolverStatus.Feasible:
                case CpSolverStatus.Optimal:
                    return;
                case CpSolverStatus.Infeasible:
                    throw new CpSolverException("The problem has been proven infeasible.");
                case CpSolverStatus.ModelInvalid:
                    throw new CpSolverException(
                        "The given CpModelProto didn't pass the validation step. You can get a detailed error by calling ValidateCpModel(model_proto).");
                case CpSolverStatus.Unknown:
                    throw new CpSolverException(
                        "The status of the model is still unknown. A search limit has been reached before any of the statuses below could be determined.");
                default:
                    throw new CpSolverException();
            }
        }

        /// <summary>
        /// Filters an isolated sub-sequence of variables assigned to True.
        /// Extract the span of Boolean variables[start, start + length), negate them,
        /// and if there is variables to the left / right of this span, surround the span by
        /// them in non negated form.
        /// </summary>
        /// <param name="works">A list of variables to extract the span from.</param>
        /// <param name="start">The start to the span.</param>
        /// <param name="length">The length of the span.</param>
        /// <returns>An array of variables which conjunction will be false if the sub-list is
        /// assigned to True, and correctly bounded by variables assigned to False,
        /// or by the start or end of works.</returns>
        static ILiteral[] NegatedBoundedSpan(IntVar[] works, int start, int length)
        {
            var sequence = new List<ILiteral>();

            if (start > 0)
                sequence.Add(works[start - 1]);

            foreach (var i in Range(length))
                sequence.Add(works[start + i].Not());

            if (start + length < works.Length)
                sequence.Add(works[start + length]);

            return sequence.ToArray();
        }

        /// <summary>
        /// Sequence constraint on true variables with soft and hard bounds.
        /// This constraint look at every maximal contiguous sequence of variables
        /// assigned to true. If forbids sequence of length &lt; hardMin or &gt; hardMax.
        /// Then it creates penalty terms if the length is &lt; softMin or &gt; softMax.
        /// </summary>
        /// <param name="model">The sequence constraint is built on this model.</param>
        /// <param name="works">A list of Boolean variables.</param>
        /// <param name="hardMin">Any sequence of true variables must have a length of at least hardMin.</param>
        /// <param name="softMin">Any sequence should have a length of at least softMin, or a linear penalty on the delta will be added to the objective.</param>
        /// <param name="minCost">The coefficient of the linear penalty if the length is less than softMin.</param>
        /// <param name="softMax">Any sequence should have a length of at most softMax, or a linear penalty on the delta will be added to the objective.</param>
        /// <param name="hardMax">Any sequence of true variables must have a length of at most hardMax.</param>
        /// <param name="maxCost">The coefficient of the linear penalty if the length is more than softMax.</param>
        /// <param name="prefix">A base name for penalty literals.</param>
        /// <returns>A tuple (costLiterals, costCoefficients) containing the different penalties created by the sequence constraint.</returns>
        static (IntVar[] costLiterals, int[] costCoefficients) AddSoftSequenceConstraint(CpModel model, IntVar[] works,
            int hardMin, int softMin, int minCost,
            int softMax, int hardMax, int maxCost, string prefix)
        {
            var costLiterals = new List<IntVar>();
            var costCoefficients = new List<int>();

            // Forbid sequences that are too short.
            foreach (var length in Range(1, hardMin))
            {
                foreach (var start in Range(works.Length - length + 1))
                {
                    model.AddBoolOr(NegatedBoundedSpan(works, start, length));
                }
            }

            // Penalize sequences that are below the soft limit.

            if (minCost > 0)
            {
                foreach (var length in Range(hardMin, softMin))
                {
                    foreach (var start in Range(works.Length - length + 1))
                    {
                        var span = NegatedBoundedSpan(works, start, length).ToList();
                        var name = $": under_span(start={start}, length={length})";
                        var lit = model.NewBoolVar(prefix + name);
                        span.Add(lit);
                        model.AddBoolOr(span);
                        costLiterals.Add(lit);
                        // We filter exactly the sequence with a short length.
                        // The penalty is proportional to the delta with softMin.
                        costCoefficients.Add(minCost * (softMin - length));
                    }
                }
            }

            // Penalize sequences that are above the soft limit.
            if (maxCost > 0)
            {
                foreach (var length in Range(softMax + 1, hardMax + 1))
                {
                    foreach (var start in Range(works.Length - length + 1))
                    {
                        var span = NegatedBoundedSpan(works, start, length).ToList();
                        var name = $": over_span(start={start}, length={length})";
                        var lit = model.NewBoolVar(prefix + name);
                        span.Add(lit);
                        model.AddBoolOr(span);
                        costLiterals.Add(lit);
                        // Cost paid is max_cost * excess length.
                        costCoefficients.Add(maxCost * (length - softMax));
                    }
                }
            }

            // Just forbid any sequence of true variables with length hardMax + 1
            foreach (var start in Range(works.Length - hardMax))
            {
                var temp = new List<ILiteral>();

                foreach (var i in Range(start, start + hardMax + 1))
                {
                    temp.Add(works[i].Not());
                }

                model.AddBoolOr(temp);
            }

            return (costLiterals.ToArray(), costCoefficients.ToArray());
        }

        /// <summary>
        /// Sum constraint with soft and hard bounds.
        /// This constraint counts the variables assigned to true from works.
        /// If forbids sum &lt; hardMin or &gt; hardMax.
        /// Then it creates penalty terms if the sum is &lt; softMin or &gt; softMax.
        /// </summary>
        /// <param name="model">The sequence constraint is built on this model.</param>
        /// <param name="works">A list of Boolean variables.</param>
        /// <param name="hardMin">Any sequence of true variables must have a length of at least hardMin.</param>
        /// <param name="softMin">Any sequence should have a length of at least softMin, or a linear penalty on the delta will be added to the objective.</param>
        /// <param name="minCost">The coefficient of the linear penalty if the length is less than softMin.</param>
        /// <param name="softMax">Any sequence should have a length of at most softMax, or a linear penalty on the delta will be added to the objective.</param>
        /// <param name="hardMax">Any sequence of true variables must have a length of at most hardMax.</param>
        /// <param name="maxCost">The coefficient of the linear penalty if the length is more than softMax.</param>
        /// <param name="prefix">A base name for penalty literals.</param>
        /// <returns>A tuple (costVariables, costCoefficients) containing the different
        /// penalties created by the sequence constraint.</returns>
        static (IntVar[] costVariables, int[] costCoefficients) AddSoftSumConstraint(CpModel model, IntVar[] works,
            int hardMin, int softMin, int minCost,
            int softMax, int hardMax, int maxCost, string prefix)
        {
            var costVariables = new List<IntVar>();
            var costCoefficients = new List<int>();
            var sumVar = model.NewIntVar(hardMin, hardMax, "");
            // This adds the hard constraints on the sum.
            model.Add(sumVar == LinearExpr.Sum(works));

            var zero = model.NewConstant(0);

            // Penalize sums below the soft_min target.

            if (softMin > hardMin && minCost > 0)
            {
                var delta = model.NewIntVar(-works.Length, works.Length, "");
                model.Add(delta == (softMin - sumVar));
                var excess = model.NewIntVar(0, works.Length, prefix + ": under_sum");
                model.AddMaxEquality(excess, new[] {delta, zero});
                costVariables.Add(excess);
                costCoefficients.Add(minCost);
            }

            // Penalize sums above the soft_max target.
            if (softMax < hardMax && maxCost > 0)
            {
                var delta = model.NewIntVar(-works.Length, works.Length, "");
                model.Add(delta == sumVar - softMax);
                var excess = model.NewIntVar(0, works.Length, prefix + ": over_sum");
                model.AddMaxEquality(excess, new[] {delta, zero});
                costVariables.Add(excess);
                costCoefficients.Add(maxCost);
            }

            return (costVariables.ToArray(), costCoefficients.ToArray());
        }

        /// <summary>
        /// C# equivalent of Python range (start, stop)
        /// </summary>
        /// <param name="start">The inclusive start.</param>
        /// <param name="stop">The exclusive stop.</param>
        /// <returns>A sequence of integers.</returns>
        static IEnumerable<int> Range(int start, int stop)
        {
            foreach (var i in Enumerable.Range(start, stop - start))
                yield return i;
        }

        /// <summary>
        /// C# equivalent of Python range (stop)
        /// </summary>
        /// <param name="stop">The exclusive stop.</param>
        /// <returns>A sequence of integers.</returns>
        static IEnumerable<int> Range(int stop)
        {
            return Range(0, stop);
        }

        private static void PrintSolution(CpSolver solver, SolverVars vars, DataModel data)
        {
            Console.WriteLine();
            if (vars.Status == CpSolverStatus.Optimal)
            {
                Console.WriteLine(
                    "Great news everyone! An optimal solution was found because we found ALL possible solutions!");
                Console.WriteLine("And now the moment you've all being waiting for...");
                Console.WriteLine();
            }

            var header = "          ";
            for (var w = 0; w < data.NumWeeks; w++)
            {
                header += "M T W T F S S ";
            }

            Console.WriteLine(header);
            var shiftLookup = new[] {'-', 'M', 'A', 'N'};
            foreach (var e in Range(data.NumEmployees))
            {
                var schedule = "";
                foreach (var d in Range(data.NumDays))
                {
                    foreach (var s in Range(data.NumShifts))
                    {
                        if (solver.BooleanValue(vars.Work[e, s, d]))
                        {
                            schedule += shiftLookup[s] + " ";
                        }
                    }
                }

                Console.WriteLine($"{data.Employees[e].PadRight(8)}: {schedule}");
            }
        }
    }
}