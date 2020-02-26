using System;
using Google.OrTools.ConstraintSolver;
using Google.Protobuf.WellKnownTypes;

namespace Back2Base.OrDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            var data = new DataModel();
            var manager = new RoutingIndexManager(data.DistanceMatrix.GetLength(0),
                data.VehicleCount,
                data.Starts,
                data.Ends);

            var routingModel = new RoutingModel(manager);

            AddDistanceConstraints(manager, routingModel, data);
            AddVehicleEndTimeConstraints(manager, routingModel, data);
            AddCapacityConstraints(manager, routingModel, data);
            AddPickupDeliveryConstraints(manager, routingModel, data);
            AddTimeWindowConstraints(manager, routingModel, data);

            var searchParameters = operations_research_constraint_solver.DefaultRoutingSearchParameters();
            searchParameters.FirstSolutionStrategy = FirstSolutionStrategy.Types.Value.PathCheapestArc;
#if DEBUG
            searchParameters.LogSearch = true;
#endif
            searchParameters.LocalSearchMetaheuristic = LocalSearchMetaheuristic.Types.Value.GuidedLocalSearch;
            searchParameters.TimeLimit = new Duration
            {
                Seconds = 2
            };
            
            var solution = routingModel.SolveWithParameters(searchParameters);

            ThrowIfUnsolved(routingModel);

            PrintSolution(data, routingModel, manager, solution);
        }

        private static void PrintSolution(
            in DataModel data,
            in RoutingModel routing,
            in RoutingIndexManager manager,
            in Assignment solution)
        {
            // Inspect solution.
            long totalDistance = 0;
            var distanceDimension = routing.GetDimensionOrDie("Distance");
            var timeDimension = routing.GetDimensionOrDie("Time");
            for (var i = 0; i < data.VehicleCount; ++i)
            {
                Console.WriteLine("Route for Vehicle {0}:", i);
                long routeDistance = 0;
                long routeLoad = 0;
                var index = routing.Start(i);
                while (true)
                {
                    long nodeIndex = manager.IndexToNode(index);
                    var demand = data.Demands[nodeIndex];
                    var symbol = '\0';
                    if (demand != 0)
                    {
                        symbol = demand > 0 ? 'P' : 'D';
                    }

                    routeLoad += demand;
                    var routeTime = solution.Min(timeDimension.CumulVar(index));
                    var distance = solution.Value(distanceDimension.CumulVar(index));

                    Console.WriteLine($"Time: {routeTime.T()} Location: {nodeIndex} Load: {routeLoad.ToString().PadRight(2)} {symbol} D: {distance/1000} km");
                    if (routing.IsEnd(index))
                    {
                        break;
                    }
                    var previousIndex = index;
                    index = solution.Value(routing.NextVar(index));
                    routeDistance += routing.GetArcCostForVehicle(previousIndex, index, 0);
                }
                Console.WriteLine("Distance of the route: {0}m", routeDistance);
                Console.WriteLine();
                totalDistance += routeDistance;
            }
            Console.WriteLine("Total distance of all routes: {0}m", totalDistance);
        }

        private static void ThrowIfUnsolved(RoutingModel routingModel)
        {
            switch (routingModel.GetStatus())
            {
                case 0:
                    throw new RoutingModelException("Problem not solved yet.");
                case 1:
                    break; // Success
                case 2:
                    throw new RoutingModelException("No solution found to the problem.");
                case 3:
                    throw new RoutingModelException("Time limit reached before finding a solution.");
                case 4:
                    throw new RoutingModelException("Model, model parameters, or flags are not valid.");
            }
        }

        private static void AddDistanceConstraints(RoutingIndexManager manager, RoutingModel model, DataModel data)
        {
            long DistanceCallBackIndex(long fromIndex, long toIndex)
            {
                var fromNode = manager.IndexToNode(fromIndex);
                var toNode = manager.IndexToNode(toIndex);

                return data.DistanceMatrix[fromNode, toNode];
            }

            var distanceCallback = model.RegisterTransitCallback(DistanceCallBackIndex);
            model.SetArcCostEvaluatorOfAllVehicles(distanceCallback);

            model.AddDimension(
                distanceCallback,
                0,
                2000 * 1000,
                true, // 0km start
                "Distance");

            var distanceDimension = model.GetMutableDimension("Distance");
            distanceDimension.SetGlobalSpanCostCoefficient(100); // minimize the length of the lognest route
        }

        private static void AddVehicleEndTimeConstraints(RoutingIndexManager manager, RoutingModel model, DataModel data)
        {
            long TransitTimeCallback(long fromIndex, long toIndex)
            {
                var fromNode = manager.IndexToNode(fromIndex);
                var toNode = manager.IndexToNode(toIndex);

                var travelTime = (int) data.TimeMatrix[fromNode, toNode];
                var serviceTime = 0;

                return serviceTime + travelTime;
            }

            int timeEvaluator = model.RegisterTransitCallback(TransitTimeCallback);

            model.AddDimensionWithVehicleCapacity(
                timeEvaluator,
                180,
                data.EndTimes,
                false, // start of the day
                "Time");
        }

        private static void AddCapacityConstraints(RoutingIndexManager manager, RoutingModel model, DataModel data)
        {
            int demandCallbackIndex = model.RegisterUnaryTransitCallback(
                (fromIndex) => {
                    var fromNode = manager.IndexToNode(fromIndex);
                    return data.Demands[fromNode];
                }
            );
            model.AddDimensionWithVehicleCapacity(
                demandCallbackIndex, 
                0,
                data.VehicleCapacities, // vehicle maximum capacities
                true,
                "Capacity");
        }

        private static void AddPickupDeliveryConstraints(RoutingIndexManager manager, RoutingModel model, DataModel data)
        {
            var distanceDimension = model.GetDimensionOrDie("Distance");

            var solver = model.solver();
            for (var i = 0; i < data.PickupsDeliveries.GetLength(0); i++)
            {
                var pickupIndex = manager.NodeToIndex(data.PickupsDeliveries[i][0]);
                var deliveryIndex = manager.NodeToIndex(data.PickupsDeliveries[i][1]);

                model.AddPickupAndDelivery(pickupIndex, deliveryIndex);
                // Ensure the same vehicle does both the pickup and delivery
                solver.Add(solver.MakeEquality(
                    model.VehicleVar(pickupIndex),
                    model.VehicleVar(deliveryIndex)));
                // Ensure pickups are done before deliveries
                solver.Add(solver.MakeLessOrEqual(
                    distanceDimension.CumulVar(pickupIndex),
                    distanceDimension.CumulVar(deliveryIndex)));
            }
        }

        private static void AddTimeWindowConstraints(RoutingIndexManager manager, RoutingModel model, DataModel data)
        {
            var timeDimension = model.GetMutableDimension("Time");

            // Add time window constraints for each location except depot.
            for (int i = 1; i < data.TimeWindows.GetLength(0); ++i)
            {
                long index = manager.NodeToIndex(i);
                timeDimension.CumulVar(index).SetRange(
                    data.TimeWindows[i, 0],
                    data.TimeWindows[i, 1]);
                model.AddToAssignment(timeDimension.SlackVar(index));
            }
            // Add time window constraints for each vehicle start node.
            for (int i = 0; i < data.VehicleCount; ++i)
            {
                long index = model.Start(i);
                timeDimension.CumulVar(index).SetRange(
                    data.TimeWindows[0, 0],
                    data.TimeWindows[0, 1]);
                model.AddToAssignment(timeDimension.SlackVar(index));
            }
            // Minimize start and end times
            for (int i = 0; i < data.VehicleCount; ++i)
            {
                model.AddVariableMaximizedByFinalizer(
                    timeDimension.CumulVar(model.Start(i)));
                model.AddVariableMinimizedByFinalizer(
                    timeDimension.CumulVar(model.End(i)));
            }
        }
    }
}
