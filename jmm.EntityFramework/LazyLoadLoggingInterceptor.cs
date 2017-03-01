using System;
using System.Data.Common;
using System.Data.Entity.Infrastructure.Interception;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace jmm.EntityFramework
{
    public class LazyLoadLoggingInterceptor : DbCommandInterceptor
    {
        private readonly Timer _logTimer;
        private readonly bool _logDuringLazyLoad;

        public LazyLoadRuntimes LazyLoadRuntimes { get; } = new LazyLoadRuntimes();

        // need separate ctors instead of using default parameter values since the type is
        // instantiated based on the number of parameters passed in the config file and
        // default parameters wouldn't suffice for supporting 0 or 1 argument ctor calls
        // ReSharper disable once UnusedMember.Global - can be called from config file
        public LazyLoadLoggingInterceptor() : this(5 * 60 * 1000) { } // default to logging every 5 minutes
        // ReSharper disable once IntroduceOptionalParameters.Global - see above comment about separate ctors
        // ReSharper disable once MemberCanBePrivate.Global - needs to be public since instantiated via reflection by EF
        public LazyLoadLoggingInterceptor(int logFrequencyInMilliseconds) : this(logFrequencyInMilliseconds, false) { } // default to not logging as they happen

        public static LazyLoadLoggingInterceptor RegisteredInstance { get; private set; }
        //private static LazyLoadLoggingInterceptor _registeredInstance;


        // ReSharper disable once MemberCanBePrivate.Global
        public LazyLoadLoggingInterceptor(int logFrequencyInMilliseconds, bool logDuringLazyLoad)
        {
            _logDuringLazyLoad = logDuringLazyLoad;
            if (logFrequencyInMilliseconds > 0) // allow turning off with any non-positive value
            {
                _logTimer = new Timer(_ => this.WriteAndResetTotals(), null, logFrequencyInMilliseconds, logFrequencyInMilliseconds);
            }
            if (RegisteredInstance == null)
            {
                DbInterception.Add(this);
                RegisteredInstance = this;
                RegisterWithEvents();
                Trace.TraceInformation($"Registered interceptor {nameof(LazyLoadLoggingInterceptor)}");
            }
        }


        private void RegisterWithEvents()
        {
            // register with anything that's going to stop the current appdomain/process/app pool/etc so we log any remaining stats
            AppDomain.CurrentDomain.ProcessExit += (sender, args) => this.WriteAndResetTotals();
            AppDomain.CurrentDomain.DomainUnload += (sender, args) => this.WriteAndResetTotals();
        }

        private void WriteAndResetTotals()
        {
            var sortedByTotalTime = this.LazyLoadRuntimes
                .GetAndClearRuntimes()
                .OrderByDescending(x => x.Value.Sum())
                .ToArray();
            if (sortedByTotalTime.Any())
            {
                var message = $"{sortedByTotalTime.Length} locations discovered performing {sortedByTotalTime.Select(x => x.Value.Count()).Sum()} lazy loads for total of {sortedByTotalTime.Select(x => x.Value.Sum()).Sum()} ms";
                WriteMessage(message);
                foreach (var entry in sortedByTotalTime)
                {
                    Trace.TraceWarning($"{entry.Key} - happened {entry.Value.Count} times for a total of {entry.Value.Sum()} ms with average of {(int)entry.Value.Average()} ms");
                }
            }
        }

        private static void WriteMessage(string message)
        {
            Trace.TraceWarning(message);
        }

        private void AddStopwatchToContext(DbCommandInterceptionContext<DbDataReader> interceptionContext, string locationDescription)
        {
            // this should move to using SetUserState/FindUserState in EF 6.2.x or later so multiple interceptors don't interfere with each other
            // https://github.com/aspnet/EntityFramework6/commit/0d4e78a4371e3e1ff3f8838b90aadaa5e53dafb0
            // this isn't available in 6.1.x so for now we're stuck with using UserState directly and just not using other interceptors at the same time.
            interceptionContext.UserState = new InFlightInfo()
            {
                LocationDescription = locationDescription,
                Stopwatch = Stopwatch.StartNew(),
            };
        }

        private class InFlightInfo
        {
            public string LocationDescription { get; set; }
            public Stopwatch Stopwatch { get; set; }
        }

        public override void ReaderExecuting(DbCommand command, DbCommandInterceptionContext<DbDataReader> interceptionContext)
        {
            // unfortunately not a better way to detect whether the load is lazy or explicit via interceptor
            var stackFrames = new StackTrace(true).GetFrames();
            var stackMethods = stackFrames?.Select(x => x.GetMethod()).ToList();

            var dynamicProxyPropertyGetterMethod = stackMethods?
                .FirstOrDefault(x =>
                    x.DeclaringType?.FullName.StartsWith("System.Data.Entity.DynamicProxies") == true &&
                    x.Name.StartsWith("get_"));
            if (dynamicProxyPropertyGetterMethod == null)
            {
                // not in a lazy-load context, nothing to do
                return;
            }

            var stackIndex = stackMethods.IndexOf(dynamicProxyPropertyGetterMethod);
            var propertyCaller = stackFrames[stackIndex + 1];

            var propertyType = dynamicProxyPropertyGetterMethod.ReflectedType?.BaseType?.Name;
            var propertyName = dynamicProxyPropertyGetterMethod.Name.Replace("get_", "");
            var locationDescription = $"{propertyCaller.GetFileName()}({propertyCaller.GetFileLineNumber()},{propertyCaller.GetFileColumnNumber()}): lazy load detected accessing navigation property {propertyName} from entity {propertyType}";

            AddStopwatchToContext(interceptionContext, locationDescription);
        }

        public override void ReaderExecuted(DbCommand command, DbCommandInterceptionContext<DbDataReader> interceptionContext)
        {
            var inFlightStopwatch = interceptionContext.UserState as InFlightInfo;

            if (inFlightStopwatch != null)
            {
                inFlightStopwatch.Stopwatch.Stop();
                var runtimesList = this.LazyLoadRuntimes.AddEntry(inFlightStopwatch.LocationDescription, inFlightStopwatch.Stopwatch.ElapsedMilliseconds);

                if (_logDuringLazyLoad)
                {
                    Trace.TraceWarning($"{inFlightStopwatch.LocationDescription} - took {inFlightStopwatch.Stopwatch.ElapsedMilliseconds} ms - total so far of {runtimesList.Count} times with {runtimesList.Sum()} ms for average of {runtimesList.Average()} ms");
                }
            }
        }
    }
}
