EntityFramework.LazyLoadLoggingInterceptor
==========================================

[![AppVeyor](https://img.shields.io/appveyor/ci/jamesmanning/entityframework-lazyloadlogginginterceptor.svg)](https://ci.appveyor.com/project/jamesmanning/entityframework-lazyloadlogginginterceptor)
[![Coveralls](https://img.shields.io/coveralls/jamesmanning/EntityFramework.LazyLoadLoggingInterceptor.svg)](https://coveralls.io/github/jamesmanning/EntityFramework.LazyLoadLoggingInterceptor)

[![GitHub issues](https://img.shields.io/github/issues/jamesmanning/EntityFramework.LazyLoadLoggingInterceptor.svg)](https://github.com/jamesmanning/EntityFramework.LazyLoadLoggingInterceptor/issues)
[![GitHub stars](https://img.shields.io/github/stars/jamesmanning/EntityFramework.LazyLoadLoggingInterceptor.svg)](https://github.com/jamesmanning/EntityFramework.LazyLoadLoggingInterceptor/stargazers)
[![GitHub forks](https://img.shields.io/github/forks/jamesmanning/EntityFramework.LazyLoadLoggingInterceptor.svg)](https://github.com/jamesmanning/EntityFramework.LazyLoadLoggingInterceptor/network)
[![GitHub license](https://img.shields.io/badge/license-MIT-blue.svg)](https://raw.githubusercontent.com/jamesmanning/EntityFramework.LazyLoadLoggingInterceptor/master/LICENSE)

[![NuGet](https://img.shields.io/nuget/v/EntityFramework.LazyLoadLoggingInterceptor.svg)](https://www.nuget.org/packages/EntityFramework.LazyLoadLoggingInterceptor/)

**TL;DR** This interceptor, which can be added via just a config change, will help identify places where your existing code is causing lazy loads to happen in Entity Framework so you can fix it, usually by add Include calls so you're doing 1 query instead of N+1 queries.

To use it with its default settings of logging lazy-load statistics every 5 minutes, you can add this child element to the <entityFramework> element present in your app.config or web.config:

    <interceptors>
      <interceptor type="EntityFramework.LazyLoadLoggingInterceptor.LazyLoadLoggingInterceptor, EntityFramework.LazyLoadLoggingInterceptor">
        <parameters>
          <parameter value="0" type="System.Int32"/> <!--disable timer-based logging-->
          <parameter value="false" type="System.Boolean"/> <!--disable at-lazy-load-time logging-->
        </parameters>
      </interceptor>
    </interceptors>

To change the frequency of logging the aggregate lazy-load data, pass in an integer constructor parameter.  It specifies the number of milliseconds the frequency of the logging will be.  If you specify zero or a negative number, the frequency-based logging will be disabled.

    <interceptors>
      <interceptor type="EntityFramework.LazyLoadLoggingInterceptor.LazyLoadLoggingInterceptor, EntityFramework.LazyLoadLoggingInterceptor">
        <parameters>
          <parameter value="86400000" type="System.Int32"/> <!-- change frequency-based logging to once a day instead of every 5 minutes-->
        </parameters>
      </interceptor>
    </interceptors>

To also have the interceptor log as each lazy load happens, add a second constructor parameter as a boolean to turn it on:

    <interceptors>
      <interceptor type="EntityFramework.LazyLoadLoggingInterceptor.LazyLoadLoggingInterceptor, EntityFramework.LazyLoadLoggingInterceptor">
        <parameters>
          <parameter value="86400000" type="System.Int32"/> <!-- change frequency-based logging to once a day instead of every 5 minutes-->
          <parameter value="true" type="System.Boolean"/> <!--enable at-lazy-load-time logging-->
        </parameters>
      </interceptor>
    </interceptors>

## Wait, what is lazy loading?

Great question, glad you asked.  [Lazy loading](https://en.wikipedia.org/wiki/Lazy_loading) is a feature of Entity Framework and many other [ORM](https://en.wikipedia.org/wiki/Object-relational_mapping)'s where one or more related entities (row(s) in another table, usually with a foreign-key relationship present) are loaded via another query being executed at the point where the entities are being accessed. It's a feature that defaults to being on in Entity Framework in particular, and sometimes the developers using EF may not realize the performance impact it might be having.

For more details about what EF6 provides for loading options, see [this MSDN page on loading related entities](https://msdn.microsoft.com/en-us/library/jj574232(v=vs.113).aspx).

## Is lazy loading always bad?

There are certainly cases where lazy loading is useful, and by providing it as a default part of the API, EF makes it much simpler for developers using EF to have things Just Work(tm) at runtime.  Without lazy loading, then many developers would often have code failing at runtime because the associated entities weren't specifically loaded so even though the associated row(s) are present in the database, the in-memory entities shows a null or empty collection instead.

With lazy loading, though, it's often the case that code written without thinking about loading related entities will often result in [N+1 queries running against the database](http://stackoverflow.com/a/97253/215534) that the developer didn't intend.

## What are the alternatives to lazy loading?

Since turning lazy loading off, especially on an existing codebase, can result in runtime breaks that didn't exist before, it's usually a bad idea to do so unless you have sufficient automated testing coverage to ensure any such places would be discovered as part of a CI build or integration testing.

In terms of API usage, there are 2 primary alternatives: [eager loading](https://msdn.microsoft.com/en-us/library/jj574232(v=vs.113).aspx#Anchor_0) and [explicit loading](https://msdn.microsoft.com/en-us/library/jj574232(v=vs.113).aspx#Anchor_2).

Eager loading is modifying the original query such that the related entities are loaded as part of the query instead of having them loaded later.  So where you might have a call like db.SomeEntities.ToList() you could change it to db.SomeEntities.Include("RelatedEntity").ToList() and the related rows in that navigation property would be populated by the same query instead of them needing to load later on.

In the cases where the related entities are going to always be needed, or usually needed, then eager-loading them usually makes sense.

If it's not a common case that the related entities will be needed, then you may have better performance by not loading them as part of the initial query, but instead loading them on an as-needed basis.  In that scenario you *could* let lazy-loading be how the related entities are loaded, but you could also explicitly load them.

## Ok, I get why lazy-loading can often be the source of performance problems including N+1 selects.  Now what?

Because lazy loading provides a useful 'safety net' in terms of loading related entities, leaving it on may still be useful for many teams.  Turning it off, as mentioned earlier, can often have unintended side effects on existing code (or even newly written code) as the related entities aren't loaded.  It's reasonable to consider the 'hindered performance' result of additional select statement(s) as the better alternative to the related entities not being loaded and causing runtime failures for customers.

So the 'best' scenario for many situations will be to leave lazy-loading turned on to provide that safety net, but have a feedback mechanism in place to help identify the lazy loads that happen at runtime so that the developers can change them, either to eager loads or to explicit loads depending on the scenario.

Now, you can use SQL profiling to see what is run from Entity Framework and often infer back which ones are caused by lazy loads, but doing so is usually based more on heuristics.  

An alternative, and what this interceptor does, is to figure out during the run of the query whether it's happening in the context of a lazy load.

A single instance of a lazy load is usually not that big of a deal, so the interceptor keeps track of the lazy loads as they happen, specifically how many times they happen from each given source location, and their total query runtime since it's synchronous to the calling context.

The interceptor logs this data via [Trace.TraceWarning](https://msdn.microsoft.com/en-us/library/system.diagnostics.trace.tracewarning(v=vs.110).aspx) calls.  It uses Trace since it's built-in to the .NET Framework and can be configured to be sent to wherever the user wants (console, file, whatever) and uses TraceWarning specifically to make it easier to distinguish from other trace traffic.

The interceptor can be configured by way of 2 optional constructor parameters.  These can be specified in the config file just like the interceptor itself so your code doesn't need to be modified.  These configure 

### logFrequencyInMilliseconds

The first optional constructor parameter is an integer of logFrequencyInMilliseconds.  If specified as 0 or negative, then there won't be any automatic logging done.  It defaults to logging every 5 minutes and will try to log any remaining/unlogged data at the point where the host process exits or the host appdomain unloads.  When it logs, it lists the total number of locations where lazy loads have happened from and total time, then lists each one of them with the number of times it happened and total and average time for each.

As an example is this log that happens (if logging is enabled) during [this unit test](https://github.com/jamesmanning/EntityFramework.LazyLoadLoggingInterceptor/blob/4f7d83194d5364f1348dc03dfae632adb676a37b/EntityFramework.LazyLoadLoggingInterceptor.Tests/LazyLoadLoggingInterceptorTests.cs#L150-L189):

```
Warning: 0 : 1 locations discovered performing 3 lazy loads for total of 0 ms
Warning: 0 : C:\github\EntityFramework.LazyLoadLoggingInterceptor\EntityFramework.LazyLoadLoggingInterceptor.Tests\LazyLoadLoggingInterceptorTests.cs(177,49): lazy load detected accessing navigation property InvoiceLineItems from entity Invoice - happened 3 times for a total of 0 ms with average of 0 ms
```

### logDuringLazyLoad

The second optional constructor parameter is a boolean of logDuringLazyLoad.  This defaults to false so that even if you're having thousands of lazy loads happen, the trace output is reasonable since it's just going to show the aggregated data every 5 minutes.  In the scenarios where you would like the trace output to include a message every time a lazy load happens (for instance, to be able to see it as part of a trace where you're tracking some overall performance issue), you can set this as true.  The frequency-based logging of aggregate data can still happen or be turned off based on the value you provide for logFrequencyInMilliseconds (see above).

## Expected Process

The expectation in using this is that you *could* have it enabled all the time, including in production. When it does the frequency-based logging, it intentionally clears all the data it has gathered so far so the interceptor isn't increasing your runtime memory usage over time.  The issue with *not* using it in production is that the access patterns and lazy loads that happen under customer load could be very different than what you see when doing testing yourself.  Production data from customer load is a much better set of numbers for driving potential performance changes, just like profiling-based optimization in general.

If you can accurately simulate/replay customer workloads in testing, then keeping the interceptor only enabled during your staging/preproduction workload may be sufficient, it will just depend on your situation.
