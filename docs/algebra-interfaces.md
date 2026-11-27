# 代数接口分层

> 原则：**先有运算，才有单位元**。  
> 0 属于加法，1 属于乘法，不绑在一起。

## 依赖顺序

```text
IAdditiveMagma<T>             + 
  └─ IAdditiveMonoid<T>       Zero
       └─ IAdditiveGroup<T>   -, 一元负

IMultiplicativeMagma<T>       *
  └─ IMultiplicativeMonoid<T> One
       └─ IMultiplicativeGroup<T>  TryInverse
```

组合：

```text
IExactRing<T>
  = IAdditiveGroup<T>
  + IMultiplicativeMonoid<T>
  + FromLong / VerifyZero / IsZeroFast / IsUnit

IComputableField<T>
  = IIntegralDomain<T>
  + IMultiplicativeGroup<T>

IRealField<T>
  = IComputableField<T> + IOrderedRing<T>

IRealSqrtField<T>
  = IRealField<T> + ISqrtable<T>

IApproxable<T>   独立：只给数值守卫，不进入正确性判定
```

## 为什么这样拆

- `Zero` 只有加法存在时才有意义；
- `One` 只有乘法存在时才有意义；
- 只做加法的算法不应被迫实现乘法；
- 只做乘法的算法不应被迫实现零判定；
- `Approx`/`Sqrt`/`Sign` 是额外能力，不是环的定义。

## 边界

- 接口只表达签名，不表达结合律/分配律/交换律/\(1\ne0\)；
- `IExactRing` / `IComputableField` / `IRealField` 这些组合名是**声明的假设**；
- 新代码应依赖最小接口，旧代码暂时继续用 `IExactRing` 兼容。
