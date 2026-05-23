using MediatR;
using ProjectManagement.Application.Interfaces;
using ProjectManagement.Domain.Entities;
using ProjectManagement.Domain.Interfaces;
using ProjectManagement.Shared.Responses;

namespace ProjectManagement.Application.Features.Auth.Commands.Register;

public sealed class RegisterCommandHandler(
    IIdentityService identityService,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RegisterCommand, ApiResponse<string>>
{
    public async Task<ApiResponse<string>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        if (await identityService.UserExistsAsync(request.Email))
            throw new InvalidOperationException("A user with this email already exists.");

        var (succeeded, userId, errors) = await identityService.RegisterAsync(
            request.Email, request.Password);

        if (!succeeded)
            throw new InvalidOperationException(string.Join("; ", errors));

        // Mirror a Domain User so project ownership FKs resolve correctly.
        var domainUser = User.Create(Guid.Parse(userId!), request.Email);
        await userRepository.AddAsync(domainUser, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<string>.Success("Registration successful. You can now log in.", "Account created.");
    }
}
