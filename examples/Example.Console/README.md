# Example .NET Console Application

This example demonstrates using the encrypted logging in a .NET Console application.

These are example keys used and you can find the public key in the `public_key.xml` file.
The private key here would be used in your logging backend to decrypt the logs.

# Example RSA Keys

```xml
<!-- Example Private Key: DO NOT USE FOR PRODUCTION -->
<RSAKeyValue>
    <Modulus>tc705LqyVsPn4IvG5m3IxrEW3ANnL008LDgTExF/1NB9gVQJiE69FceFxStt4X33FuOVdUQQxeG80GHH/Pz29HnjeoFYhdcVmBhMpkBheHMXKS0jTcxm8/tqu9e1cjf0f2SRElJi2JTfuIm19cfHSeC+nbBIeWoLMG33BmCBO4Ee5o8STfcZ0ZkGOupLEs7iM1lfCjbhJKfRVcSNkStz2UvJWpQAAs5bZVO824wURTM1cz64tkgkXaI+TRJERSl87wZVKBAUKCAZvu9ibbXmsSX4nRiACvuq1wq8+Vtv7yUeymkx0aHi+GcKelrgf8OQukEobvm1g2/Yar1DFM4E4Q==</Modulus>
    <Exponent>AQAB</Exponent>
    <P>xMlZ0jVeATXLeayxPjU8i1umagMiSKIrf748XhQ60i/xtr1R1772PO48tl1hrylDhR1YKzzcDr3AUYNnjfaKqrLzpg3ppDgBqaaHpkI2zZ4H6EVH43dMh4bdqM+KoOTTf27T1MnfDAzlS72veIuCPOSLubjjBY4zyBQQcomGTQc=</P>
    <Q>7IPUHjp6eYSul04PGeTcdEPwH9zqzvtrEMlUV58PH1TTFaJykew5qAMuBKBq57CegaAnT/DKF/uC7PE4dHsXAt64vwelPMjMSJ+KF0Hp8XrYYXju9YBXhUiT0CAX4linZkW5MsQwiLTf6uKvyeJYeSQpNhpZdn3P1ku1smihDNc=</Q>
    <DP>hUkv2k4lRsKN9/K89jqOsIE6HRHUVcfpmJNcRgiDsrughzqBxZRlKe3fF1H2iPJ09iNjBZ4qGp3xNgS/zgrP70BYAP/pYT5B+gt+U2EDx48jIfJS98FlB2KUnfM8nS+ABQ8m0TiOJ5ZQdFWdFRPA+cd+0CNnOOsRLZJRdZKqcV8=</DP>
    <DQ>FZwl8W0ea6P339M+BfunOOhMNF0XBzppmesLTcY2t4Ikl0F0fmi7C+LefYW6vtigYu+jJ+a0UGSmncechOgKPjTjZHDqZBdksu4AzAc1vN6gjqdiDgRf+sma19VW2VOIWAve1Aig9pywKOH1ddi8hLUNHuhhbDYqm0XnoBl3PZk=</DQ>
    <InverseQ>n5Bkq65MoFWtGD7ejLqJZD/6x2TzkD2TA15v70+hJd7ROgKeYMPH/kcyFEIZFm3JCLf6H8OTHDWdTuF1AVz3UADQy7lWZs5LnYfmDtVxXnexH+TWMPUalQCX0m/O8RXKsaLeBwXfZ4lpWoPUoE76mEBW45nADdqmTJhkzX84NwU=</InverseQ>
    <D>LnBilVoq24bG39BUSd+Y/UeWszQbUzXDPPhFxwUdRdrLdBFnLe4O1lVUkfx54LMwPiR7tKxiLGlwtTmEv9ZNrjzEIjKPzgAWf/cU/RJM01tyew4Qinq/gWqi5MBAlKA+T5v8glWQ8gl068ohNFqHSROjHNw+ZuTzXrd39aUI3Y27DQaGhBoGQP2jX3RXn7e13FBT6qUp2NpLV2Ber0o+5CBm1hnYKSFM2GaYF3AmNRdTexESQqg+BZJOf5cPwVxbV1j1vYRK8KxHcNQMn619o6OI+11gM8yZjQ9VFbaqnucAFdla4k+ossO7PV7MDX8Q/XD3tLAs0ZhPayWcCPXRkQ==</D>
</RSAKeyValue>
```

```xml
<!-- Example Public Key: DO NOT USE FOR PRODUCTION -->
<RSAKeyValue>
    <Modulus>tc705LqyVsPn4IvG5m3IxrEW3ANnL008LDgTExF/1NB9gVQJiE69FceFxStt4X33FuOVdUQQxeG80GHH/Pz29HnjeoFYhdcVmBhMpkBheHMXKS0jTcxm8/tqu9e1cjf0f2SRElJi2JTfuIm19cfHSeC+nbBIeWoLMG33BmCBO4Ee5o8STfcZ0ZkGOupLEs7iM1lfCjbhJKfRVcSNkStz2UvJWpQAAs5bZVO824wURTM1cz64tkgkXaI+TRJERSl87wZVKBAUKCAZvu9ibbXmsSX4nRiACvuq1wq8+Vtv7yUeymkx0aHi+GcKelrgf8OQukEobvm1g2/Yar1DFM4E4Q==</Modulus>
    <Exponent>AQAB</Exponent>
</RSAKeyValue>
```