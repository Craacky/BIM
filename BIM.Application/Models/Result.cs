using BIM.Application.Common.Interfaces;

namespace BIM.Application.Models
{
    public class Result : IResult
    {
        public Result()
        {
            Errors = new string[] { };
        }
        public Result(IEnumerable<string> errors, bool succeeded)
        {
            Errors = errors.ToArray();
            Succeeded = succeeded;
        }
        public string[] Errors { get; set; }
        public bool Succeeded { get; set; }
        public string ErrorMessage => string.Join(", ", Errors ?? new string[] { });

        public static Result Success()
            => new Result(Array.Empty<string>(), true);

        public static Task<Result> SuccessAsync()
            => Task.FromResult(new Result(Array.Empty<string>(), true));

        public static Result Fail(IEnumerable<string> errors)
            => new Result(errors, false);

        public static Task<Result> FailAsync(IEnumerable<string> errors)
            => Task.FromResult(new Result(errors, false));
    }

    public class Result<T> : Result, IResult<T>
    {
        public T? Data { get; set; }

        public static Result<T> Fail(IEnumerable<string> errors)
            => new Result<T>
            {
                Succeeded = false,
                Errors = errors.ToArray()
            };

        public static new async Task<Result<T>> FailAsync(IEnumerable<string> errors)
            => await Task.FromResult(Fail(errors));

        public static Result<T> Success(T data)
            => new Result<T>
            {
                Succeeded = true,
                Data = data
            };

        public static async Task<Result<T>> SuccessAsync(T data)
            => await Task.FromResult(Success(data));
    }
}
