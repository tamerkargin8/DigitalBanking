using AutoMapper;
using DigitalBanking.Application.DTOs.Account;
using DigitalBanking.Application.DTOs.Customers;
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
    }
}
