using System;
using System.Data.Common;
using System.Data.Entity.Infrastructure.Interception;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EntityFramework.LazyLoadLoggingInterceptor
{
    public class LazyLoadLoggingInterceptor : DbCommandInterceptor, IDisposable
    {
        private static readonly TraceSource _traceSource = new TraceSource("EntityFramework.LazyLoadLoggingInterceptor");
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
                RegisteredInstance = this;
                RegisterAppDomainEvents();
                _traceSource.TraceInformation($"Registered interceptor {nameof(LazyLoadLoggingInterceptor)}");
            }
        }

        public void Dispose()
        {
            if (RegisteredInstance == this)
            {
                RegisteredInstance = null;
                UnregisterAppDomainEvents();
                _logTimer?.Dispose();
                _traceSource.TraceInformation($"Unregistered interceptor {nameof(LazyLoadLoggingInterceptor)}");
            }
        }

        private void RegisterAppDomainEvents()
        {
            // register with anything that's going to stop the current appdomain/process/app pool/etc so we log any remaining stats
            AppDomain.CurrentDomain.ProcessExit += WriteAndResetTotalsEventHandler;
            AppDomain.CurrentDomain.DomainUnload += WriteAndResetTotalsEventHandler;
        }

        private void UnregisterAppDomainEvents()
        {
            // register with anything that's going to stop the current appdomain/process/app pool/etc so we log any remaining stats
            AppDomain.CurrentDomain.ProcessExit -= WriteAndResetTotalsEventHandler;
            AppDomain.CurrentDomain.DomainUnload -= WriteAndResetTotalsEventHandler;
        }

        private void WriteAndResetTotalsEventHandler(object sender, EventArgs args) => this.WriteAndResetTotals();

        private void WriteAndResetTotals()
        {
            var sortedByTotalTime = this.LazyLoadRuntimes
                .GetAndClearRuntimes()
                .OrderByDescending(x => x.Value.Sum())
                .ToArray();
            if (sortedByTotalTime.Any())
            {
                var message = $"{sortedByTotalTime.Length} locations discovered performing {sortedByTotalTime.Select(x => x.Value.Count()).Sum()} lazy loads for total of {sortedByTotalTime.Select(x => x.Value.Sum()).Sum()} ms";
                _traceSource.TraceInformation(message);
                foreach (var entry in sortedByTotalTime)
                {
                    _traceSource.TraceInformation($"{entry.Key} - happened {entry.Value.Count} times for a total of {entry.Value.Sum()} ms with average of {(int)entry.Value.Average()} ms");
                }
                _traceSource.Flush();
            }
        }

        private void AddStopwatchToContext(DbCommandInterceptionContext<DbDataReader> interceptionContext, string locationDescription)
        {
            interceptionContext.SetUserState(nameof(LazyLoadLoggingInterceptor), new InFlightInfo()
            {
                LocationDescription = locationDescription,
                Stopwatch = Stopwatch.StartNew(),
            });
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
            var inFlightStopwatch = interceptionContext.FindUserState(nameof(LazyLoadLoggingInterceptor)) as InFlightInfo;

            if (inFlightStopwatch != null)
            {
                inFlightStopwatch.Stopwatch.Stop();
                var runtimesList = this.LazyLoadRuntimes.AddEntry(inFlightStopwatch.LocationDescription, inFlightStopwatch.Stopwatch.ElapsedMilliseconds);

                if (_logDuringLazyLoad)
                {
                    _traceSource.TraceInformation($"{inFlightStopwatch.LocationDescription} - took {inFlightStopwatch.Stopwatch.ElapsedMilliseconds} ms - total so far of {runtimesList.Count} times with {runtimesList.Sum()} ms for average of {runtimesList.Average()} ms");
                }
            }
        }
    }
}
