# Código de seguridad

## Para qué sirve

Si alguien interceptara el QR y se emparejara haciéndose pasar por tu contacto, todo funcionaría,
pero con **su** clave. El código de seguridad lo detecta: son 60 dígitos calculados a partir de las
dos claves de identidad, y **ambos teléfonos deben mostrar los mismos**.

```text
12345 67890 13579 24680 11223 34455
66778 89900 10293 84756 56473 82910
```

## Cómo se calcula

Sigue el diseño del *safety number* de Signal:

1. Para cada clave de identidad: `SHA-512(versión ‖ clave ‖ "Directo")` y después **5 200** veces
   `SHA-512(hash ‖ clave)`. Las iteraciones encarecen buscar una clave falsa con un código parecido.
2. Se toman 30 bytes, en 6 bloques de 5, y cada bloque se convierte en 5 dígitos → 30 dígitos por persona.
3. Se concatenan las dos mitades **en orden** para que ambos teléfonos muestren lo mismo.

## Cómo usarlo

Comparadlo una vez, en persona o por un canal de confianza, y marcad el contacto como
**verificado**. Mientras no lo hagáis, la app lo indica en el chat.

**En el código:** [`SafetyNumber.cs`](https://github.com/fsoftt/Directo/blob/main/src/Directo.Security/Identity/SafetyNumber.cs) ·
[`ContactDetailsViewModel.cs`](https://github.com/fsoftt/Directo/blob/main/src/Directo.Presentation/ViewModels/ContactDetailsViewModel.cs)
