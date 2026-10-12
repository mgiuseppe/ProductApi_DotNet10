namespace Application.Common.Behaviors;

// THIS FILE IS JUST AN EXAMPLE FOR REFERENCE OF HOW TO IMPLEMENT A BEHAVIOR
// I WON'T USE THIS FILE IN THE PROJECT BECAUSE I DON'T LIKE THE IDEA OF CALLING SaveChangesAsync AFTER THE HANDLER IS EXECUTED

// ICommand.cs
// 1) Create marker interface for commands.
// 
// public interface ICommandBase;
// public interface ICommand : IRequest, ICommandBase;
// public interface ICommand<TResponse> : IRequest<TResponse>, ICommandBase;

// 2) Change your commands to implement ICommand/ICommand<TResponse> instead of IRequest<TResponse>
// 
// public record CreateProductCommand(string Name, decimal Price, string? Description) : ICommand<ProductResponse>;
// public record DeleteProductCommand(Guid Id) : ICommand;

// 3) Create the Behavior implementing IPipelineBehavior and injecting IUnitOfWork in the constructor
//    This behavior will apply to all command that implement ICommandBase
//    This behavior will call SaveChangesAsync after the handler is executed
// Limits:
// - SaveChangesAsync is invisible in the handler because it will be magically called after the handler is executed
// - If you have id/row_version db-side generated, these won't be available in the handler since SaveChangesAsync is called after the handler is executed
// - Commit exceptions will be thrown in the behavior and will have a different stack trace than if they were thrown in the handler
//
// internal class UnitOfWorkBehavior<TRequest, TResponse>(IUnitOfWork uow)
//     : IPipelineBehavior<TRequest, TResponse>
//     where TRequest : ICommandBase
// {
//     public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
//     {
//         var response = await next(ct);   // runs the handler
//         await uow.SaveChangesAsync(ct);
//         return response;
//     }
// }

// DependencyInjection.cs
// 4) Register the behavior in the DI Container 
// 
// services.AddMediatR(cfg =>
// {
//     cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
//     cfg.AddOpenBehavior(typeof(UnitOfWorkBehavior<,>));
// });

// 5) Remove the SaveChangesAsync call and the IUnitOfWork injection from the handlers
