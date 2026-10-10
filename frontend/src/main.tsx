import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createBrowserRouter, RouterProvider } from 'react-router'
import { App } from './App'
import { SesionProvider } from './auth/Sesion'
import './estilos.css'

const router = createBrowserRouter([{ path: '*', element: <SesionProvider><App /></SesionProvider> }])

createRoot(document.getElementById('root')!).render(
  <StrictMode><RouterProvider router={router} /></StrictMode>,
)
