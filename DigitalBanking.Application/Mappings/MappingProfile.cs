using AutoMapper;
using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.Account;
using DigitalBanking.Application.DTOs.Customers;
using DigitalBanking.Application.DTOs.PaymentApprovals;
using DigitalBanking.Application.DTOs.Transaction;
using DigitalBanking.Domain.Entities;

namespace DigitalBanking.Application.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Customer, CustomerResponse>();
        CreateMap<CreateCustomerRequest, Customer>();

        CreateMap<Account, AccountDto>();

        CreateMap<Transaction, TransactionDto>();

        CreateMap<TransferResult, TransferResultDto>();
        CreateMap<OpenAccountRequest, CreateAccountRequest>();
        CreateMap<PaymentApproval, PaymentApprovalResponse>()
            .ForMember(dest => dest.Reference, opt => opt.MapFrom(src => Masking.PaymentReference(src.Id, src.CorrelationId)))
            .ForMember(dest => dest.RequestType, opt => opt.MapFrom(src => src.RequestType.ToString()))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
            .ForMember(dest => dest.RiskLevel, opt => opt.MapFrom(src => src.RiskLevel.ToString()));
    }
}
