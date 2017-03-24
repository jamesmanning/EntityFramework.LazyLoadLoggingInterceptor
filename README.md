EntityFramework.LazyLoadLoggingInterceptor
==========================================

[![AppVeyor](https://img.shields.io/appveyor/ci/jamesmanning/entityframework-lazyloadlogginginterceptor.svg)](https://ci.appveyor.com/project/jamesmanning/entityframework-lazyloadlogginginterceptor)
[![Coveralls](https://img.shields.io/coveralls/jamesmanning/EntityFramework.LazyLoadLoggingInterceptor.svg)](https://coveralls.io/github/jamesmanning/EntityFramework.LazyLoadLoggingInterceptor)

[![GitHub issues](https://img.shields.io/github/issues/jamesmanning/EntityFramework.LazyLoadLoggingInterceptor.svg)](https://github.com/jamesmanning/EntityFramework.LazyLoadLoggingInterceptor/issues)
[![GitHub stars](https://img.shields.io/github/stars/jamesmanning/EntityFramework.LazyLoadLoggingInterceptor.svg)](https://github.com/jamesmanning/EntityFramework.LazyLoadLoggingInterceptor/stargazers)
[![GitHub forks](https://img.shields.io/github/forks/jamesmanning/EntityFramework.LazyLoadLoggingInterceptor.svg)](https://github.com/jamesmanning/EntityFramework.LazyLoadLoggingInterceptor/network)
[![GitHub license](https://img.shields.io/badge/license-MIT-blue.svg)](https://raw.githubusercontent.com/jamesmanning/EntityFramework.LazyLoadLoggingInterceptor/master/LICENSE)

[![NuGet](https://img.shields.io/nuget/v/EntityFramework.LazyLoadLoggingInterceptor.svg)](https://www.nuget.org/packages/EntityFramework.LazyLoadLoggingInterceptor/)

**TL;DR** This interceptor will help identify places where your existing code is causing lazy loads to happen in Entity Framework so you can fix it, usually by add Include calls so you're doing 1 query instead of N+1 queries.  The NuGet package will add this interceptor to your entityFramework element inside your app.config or web.config file for you, so after installing the package you can just check out the trace logging to see the lazy load statistics.

### What if I want to modify the configuration?

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

## Can you show me an example of using this?

That's a great idea.  We'll use the old but somewhat canonical Northwind database (customers, orders, products, etc) since that should be familiar enough as a data model for most.

Let's say we decided to write this program to find the best customer and print out the info about their orders and items within the orders:

```csharp
static void Main(string[] args)
{
    using (var db = new Northwind())
    {
        var stopwatch = Stopwatch.StartNew();
        var bestCustomer = db.Customers
            .OrderByDescending(customer => customer.Orders.Count)
            .First();

        Console.WriteLine($"Best customer is {bestCustomer.CompanyName}");
        foreach (var customerOrder in bestCustomer.Orders)
        {
            Console.WriteLine($"\tPlaced order {customerOrder.OrderID} with {customerOrder.Order_Details.Count} line items on date {customerOrder.OrderDate:d}");
            foreach (var orderDetail in customerOrder.Order_Details)
            {
                Console.WriteLine($"\t\t{orderDetail.Quantity} of {orderDetail.Product.ProductName} from category {orderDetail.Product.Category.CategoryName} at a cost of {orderDetail.UnitPrice:C} each");
            }
        }
        Console.WriteLine($"Finished printing out info in {stopwatch.Elapsed}");
    }
}
```

Certainly this is a bit of a contrived example, but hopefully it serves the purpose.

If we just run this as-is, it works fine. For me it runs in 10 seconds. 

![start of output](http://i.imgur.com/8XScrBi.png)

![end of output](http://i.imgur.com/KAjCrMv.png)

Now, our code is working, we can move on to the next entry in our queue of work items.  Maybe it's later on that we need it to run faster or we notice it's making far more queries than we would have expected since we're only printing the information from a single customer.

With the NuGet package, it's meant so you can use it "out of the box" with sane defaults - it logs to both a file on disk and the console, along with the default trace viewer of course (so you can use [DebugView](https://technet.microsoft.com/en-us/sysinternals/debugview.aspx) or the like if you wanted).

You can either use the 'Manage NuGet Packages' UI

![showing package in nuget UI](http://i.imgur.com/sRDM4Px.png)

Or use the Package Manager Console and run `Install-Package EntityFramework.LazyLoadLoggingInterceptor`

With no other changes, just installing the nuget package (which modifies the config file for you to add the interceptor and trace source and listeners), I can run the same program again and now at the end it'll tell me about all those lazy loads that happened:

```
4 locations discovered performing 93 lazy loads for total of 5807 ms
C:\github\Northwind.EF6\ConsoleApplication1\Program.cs(27,25): lazy load detected accessing navigation property Product from entity Order_Detail - happened 53 times for a total of 3332 ms with average of 62 ms
C:\github\Northwind.EF6\ConsoleApplication1\Program.cs(24,21): lazy load detected accessing navigation property Order_Details from entity Order - happened 31 times for a total of 1848 ms with average of 59 ms
C:\github\Northwind.EF6\ConsoleApplication1\Program.cs(27,25): lazy load detected accessing navigation property Category from entity Product - happened 8 times for a total of 492 ms with average of 61 ms
C:\github\Northwind.EF6\ConsoleApplication1\Program.cs(22,47): lazy load detected accessing navigation property Orders from entity Customer - happened 1 times for a total of 135 ms with average of 135 ms
```

So we "felt" like we did a single query in looking up the customer, but as written, 93 more queries happened while we iterated under that customer's orders. Yikes.

The default settings don't write a log entry every time a lazy load happens, but does include writing the statistics to the trace output like the above.

Of note is that the source code locations in the output are in the format that Visual Studio supports for allowing you to double-click a link to take you to the relevant place in the source.  The numbers in the parentheses are the line number and column number.

If we wanted to do an Include so all the needed entities were loading with the first query and no lazy loads would happen, we could just add this one Include call:

```csharp
var bestCustomer = db.Customers
    .Include("Orders.Order_Details.Product.Category")
    .OrderByDescending(customer => customer.Orders.Count)
    .First();
```

And now we're back down to a single query hitting the database.

However, how you choose to fix the lazy loads (eager loading, explicit loading, whatever) is up to you, but the interceptor has let you know about them so you can better understand your performance.  For the hopefully uncommon case of wanting to avoid eager loading, you can change to an explicit load.  That both makes it more obvious to future readers of the code what the runtime query behavior is and lets you continue to use the interceptor to identify the unintentional lazy loads happening in your code.
